// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
//! Minimal Valve VDF / ACF parser for Steam library and app manifests.

use std::collections::HashMap;

/// Parse a simple VDF document into nested string maps.
/// Good enough for `libraryfolders.vdf` and `appmanifest_*.acf`.
pub fn parse_vdf(input: &str) -> HashMap<String, VdfValue> {
    let tokens = tokenize(input);
    let mut idx = 0;
    parse_object(&tokens, &mut idx)
}

#[derive(Debug, Clone)]
pub enum VdfValue {
    Str(String),
    Obj(HashMap<String, VdfValue>),
}

impl VdfValue {
    pub fn as_str(&self) -> Option<&str> {
        match self {
            VdfValue::Str(s) => Some(s),
            _ => None,
        }
    }

    pub fn as_obj(&self) -> Option<&HashMap<String, VdfValue>> {
        match self {
            VdfValue::Obj(o) => Some(o),
            _ => None,
        }
    }

    pub fn get_str(&self, key: &str) -> Option<&str> {
        self.as_obj()?.get(key)?.as_str()
    }

    #[allow(dead_code)]
    pub fn get_obj(&self, key: &str) -> Option<&HashMap<String, VdfValue>> {
        self.as_obj()?.get(key)?.as_obj()
    }
}

fn tokenize(input: &str) -> Vec<Token> {
    let mut tokens = Vec::new();
    let bytes = input.as_bytes();
    let mut i = 0;
    while i < bytes.len() {
        match bytes[i] {
            b' ' | b'\t' | b'\r' | b'\n' => i += 1,
            b'/' if i + 1 < bytes.len() && bytes[i + 1] == b'/' => {
                while i < bytes.len() && bytes[i] != b'\n' {
                    i += 1;
                }
            }
            b'{' => {
                tokens.push(Token::LBrace);
                i += 1;
            }
            b'}' => {
                tokens.push(Token::RBrace);
                i += 1;
            }
            b'"' => {
                i += 1;
                let start = i;
                let mut s = String::new();
                while i < bytes.len() {
                    if bytes[i] == b'\\' && i + 1 < bytes.len() {
                        i += 1;
                        s.push(bytes[i] as char);
                        i += 1;
                        continue;
                    }
                    if bytes[i] == b'"' {
                        i += 1;
                        break;
                    }
                    s.push(bytes[i] as char);
                    i += 1;
                }
                // If empty and we only advanced past quotes
                let _ = start;
                tokens.push(Token::String(s));
            }
            _ => {
                // bare word (rare in modern VDF)
                let start = i;
                while i < bytes.len()
                    && !matches!(bytes[i], b' ' | b'\t' | b'\r' | b'\n' | b'{' | b'}' | b'"')
                {
                    i += 1;
                }
                let s = String::from_utf8_lossy(&bytes[start..i]).into_owned();
                if !s.is_empty() {
                    tokens.push(Token::String(s));
                }
            }
        }
    }
    tokens
}

#[derive(Debug)]
enum Token {
    String(String),
    LBrace,
    RBrace,
}

fn parse_object(tokens: &[Token], idx: &mut usize) -> HashMap<String, VdfValue> {
    let mut map = HashMap::new();
    while *idx < tokens.len() {
        match &tokens[*idx] {
            Token::RBrace => {
                *idx += 1;
                break;
            }
            Token::String(key) => {
                let key = key.clone();
                *idx += 1;
                if *idx >= tokens.len() {
                    break;
                }
                match &tokens[*idx] {
                    Token::LBrace => {
                        *idx += 1;
                        let obj = parse_object(tokens, idx);
                        map.insert(key, VdfValue::Obj(obj));
                    }
                    Token::String(val) => {
                        map.insert(key, VdfValue::Str(val.clone()));
                        *idx += 1;
                    }
                    Token::RBrace => break,
                }
            }
            Token::LBrace => {
                *idx += 1;
                let _ = parse_object(tokens, idx);
            }
        }
    }
    map
}

/// Extract library folder paths from libraryfolders.vdf content.
pub fn library_paths(vdf_text: &str) -> Vec<String> {
    let root = parse_vdf(vdf_text);
    let mut paths = Vec::new();

    // Modern format: "libraryfolders" { "0" { "path" "..." } }
    if let Some(libs) = root
        .get("libraryfolders")
        .and_then(|v| v.as_obj())
        .or_else(|| root.values().next().and_then(|v| v.as_obj()))
    {
        for (_k, v) in libs {
            if let Some(path) = v.get_str("path") {
                paths.push(path.replace("\\\\", "\\"));
            } else if let Some(s) = v.as_str() {
                // older format values can be bare paths in nested structure
                if s.contains(':') || s.starts_with('/') {
                    paths.push(s.replace("\\\\", "\\"));
                }
            }
        }
    }

    paths
}

/// Parse appmanifest fields: appid, name, installdir.
pub fn parse_appmanifest(text: &str) -> Option<(String, String, String)> {
    let root = parse_vdf(text);
    let app = root
        .get("AppState")
        .or_else(|| root.values().next())?
        .as_obj()?;

    let appid = app.get("appid")?.as_str()?.to_string();
    let name = app
        .get("name")
        .and_then(|v| v.as_str())
        .unwrap_or("Unknown")
        .to_string();
    let installdir = app.get("installdir")?.as_str()?.to_string();
    Some((appid, name, installdir))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_libraryfolders_modern() {
        let sample = r#"
"libraryfolders"
{
	"0"
	{
		"path"		"C:\\Program Files (x86)\\Steam"
		"label"		""
	}
	"1"
	{
		"path"		"D:\\SteamLibrary"
	}
}
"#;
        let paths = library_paths(sample);
        assert_eq!(paths.len(), 2);
        assert!(paths.iter().any(|p| p.contains("Steam")));
        assert!(paths.iter().any(|p| p.contains("SteamLibrary")));
    }

    #[test]
    fn parse_appmanifest_sample() {
        let sample = r#"
"AppState"
{
	"appid"		"730"
	"Universe"		"1"
	"name"		"Counter-Strike 2"
	"StateFlags"		"4"
	"installdir"		"Counter-Strike Global Offensive"
}
"#;
        let (id, name, dir) = parse_appmanifest(sample).unwrap();
        assert_eq!(id, "730");
        assert_eq!(name, "Counter-Strike 2");
        assert_eq!(dir, "Counter-Strike Global Offensive");
    }
}
