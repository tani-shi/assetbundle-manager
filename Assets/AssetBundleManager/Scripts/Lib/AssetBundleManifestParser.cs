using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AssetBundleManager.Lib {
    [SerializableAttribute]
    public class ManifestInfo {
        public List<string> assets = new List<string> ();
        public List<string> dependencies = new List<string> ();
        public uint crc;
        public Hash128 assetFileHash;
        public Hash128 typeTreeHash;
    }

    public class AssetBundleManifestParser {
        public Dictionary<string, ManifestInfo> manifestInfoDictionary = new Dictionary<string, ManifestInfo> ();

        static readonly string prefixOfCrc = "CRC: ";
        static readonly string prefixOfHash = "    Hash: ";
        static readonly string prefixOfListItem = "- ";
        static readonly string prefixOfHashesScope = "Hashes:";
        static readonly string prefixOfAssetFileHashScope = "  AssetFileHash:";
        static readonly string prefixOfTypeTreeHashScope = "  TypeTreeHash:";
        static readonly string prefixOfAssetsScope = "Assets:";
        static readonly string prefixOfDependencies = "Dependencies:";

        enum Scope {
            None,
            Crc,
            Hashes,
            AssetFileHash,
            TypeTreeHash,
            Assets,
            Dependencies,
        }

        public AssetBundleManifestParser (string outputPath, AssetBundleManifest manifest) {
            foreach (var assetBundleName in manifest.GetAllAssetBundles ()) {
                var info = new ManifestInfo ();
                if (TryParse (outputPath, assetBundleName, info)) {
                    manifestInfoDictionary.Add (assetBundleName, info);
                } else {
                    break;
                }
            }
        }

        private bool TryParse (string outputPath, string assetBundleName, ManifestInfo info) {
            try {
                var path = Path.Combine (outputPath, assetBundleName);
                using (var reader = new StreamReader (path + ".manifest")) {
                    var scope = Scope.None;
                    while (reader.Peek () > 0) {
                        var line = reader.ReadLine ();
                        if (GetScope (line) != Scope.None) {
                            scope = GetScope (line);
                        }
                        switch (scope) {
                            case Scope.Crc:
                                info.crc = uint.Parse (line.Substring (prefixOfCrc.Length));
                                break;
                            case Scope.Hashes:
                                if (line.StartsWith (prefixOfAssetFileHashScope)) {
                                    scope = Scope.AssetFileHash;
                                } else if (line.StartsWith (prefixOfTypeTreeHashScope)) {
                                    scope = Scope.TypeTreeHash;
                                }
                                break;
                            case Scope.AssetFileHash:
                                if (line.StartsWith (prefixOfHash)) {
                                    info.assetFileHash = Hash128.Parse (line.Substring (prefixOfHash.Length));
                                }
                                break;
                            case Scope.TypeTreeHash:
                                if (line.StartsWith (prefixOfHash)) {
                                    info.typeTreeHash = Hash128.Parse (line.Substring (prefixOfHash.Length));
                                }
                                break;
                            case Scope.Assets:
                                if (line.StartsWith (prefixOfListItem)) {
                                    info.assets.Add (line.Substring (prefixOfListItem.Length));
                                }
                                break;
                            case Scope.Dependencies:
                                if (line.StartsWith (prefixOfListItem)) {
                                    info.dependencies.Add (line.Substring (prefixOfListItem.Length + outputPath.Length + 1));
                                }
                                break;
                        }
                    }
                }
            } catch (Exception e) {
                Debug.LogError (e);
                return false;
            }
            return true;
        }

        private Scope GetScope (string line) {
            if (line.StartsWith (prefixOfCrc)) {
                return Scope.Crc;
            } else if (line.Equals (prefixOfHashesScope)) {
                return Scope.Hashes;
            } else if (line.Equals (prefixOfAssetsScope)) {
                return Scope.Assets;
            } else if (line.Equals (prefixOfDependencies)) {
                return Scope.Dependencies;
            }
            return Scope.None;
        }
    }
}