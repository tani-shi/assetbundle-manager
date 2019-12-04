using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetBundleManager.Lib;
using UnityEditor;
using UnityEngine;

namespace AssetBundleManager.Editor {
    public class AssetBundleBuilder : AssetPostprocessor {
        public static void RefreshAllAssetBundleNames (IAssetBundleHelper helper) {
            EditorUtility.DisplayProgressBar (string.Empty, "Searching All Assets...", 0.0f);

            try {
                var files = GetAllAssetBundleFiles (helper);
                var rate = 1.0f / (float) files.Length;

                // Process all files
                for (int i = 0, n = files.Length; i < n; i++) {
                    EditorUtility.DisplayProgressBar (string.Empty, string.Format ("In processing All Resources... {0} / {1}", i, n), (float) i * rate);
                    SetAssetBundleName (files[i], helper.GetAssetBundleName (files[i]));
                }

                AssetDatabase.Refresh ();
                AssetDatabase.SaveAssets ();
            } catch (Exception e) {
                Debug.LogError (e);
            }

            EditorUtility.ClearProgressBar ();
        }

        public static void Build (IAssetBundleHelper helper) {
            var buildDestPath = helper.GetBuildDestPath ();
            Directory.CreateDirectory (buildDestPath);

            var buildCollectionDestPath = helper.GetBuildCollectionDestPath ();
            Directory.CreateDirectory (buildCollectionDestPath);

            var collectionPath = helper.GetCollectionAssetPath ();
            Directory.CreateDirectory (collectionPath);

            Build (helper.GetBuildTarget (), buildDestPath, buildCollectionDestPath, helper.GetCollectionName (), collectionPath);
        }

        private static void Build (BuildTarget target, string destPath, string collectionDestPath, string collectionBundleName, string collectionPath) {
            var manifest = BuildPipeline.BuildAssetBundles (destPath, BuildAssetBundleOptions.IgnoreTypeTreeChanges, target);
            if (manifest != null) {
                CreateAssetBundleCollection (manifest, target, destPath, collectionPath);
                var map = new AssetBundleBuild[1];
                map[0].assetBundleName = collectionBundleName;
                map[0].assetNames = new string[] { collectionPath };
                BuildPipeline.BuildAssetBundles (collectionDestPath, map, BuildAssetBundleOptions.None, target);
            }
        }

        private static void CreateAssetBundleCollection (AssetBundleManifest manifest, BuildTarget target, string destPath, string collectionPath) {
            var collection = ScriptableObject.CreateInstance<AssetBundleInfoCollection> ();
            AssetDatabase.CreateAsset (collection, collectionPath);
            var parser = new AssetBundleManifestParser (destPath, manifest);
            foreach (var kv in parser.manifestInfoDictionary) {
                var assetBundleInfo = new AssetBundleInfoCollection.Info ();
                collection.list.Add (assetBundleInfo);
                assetBundleInfo.assetBundleName = kv.Key;
                assetBundleInfo.manifestInfo = kv.Value;
                var fileInfo = new FileInfo (Path.Combine (destPath, kv.Key));
                assetBundleInfo.size = (ulong) fileInfo.Length;
            }
            EditorUtility.SetDirty (collection);
        }

        private static void SetAssetBundleName (string path, string name) {
            var importer = AssetImporter.GetAtPath (path);
            if (File.Exists (path)) {
                if (importer != null && !importer.assetBundleName.Equals (name)) {
                    importer.assetBundleName = name;
                    importer.SaveAndReimport ();
                }
            }
        }

        private static string[] GetAllAssetBundleFiles (IAssetBundleHelper helper) {
            var list = new List<string> ();
            foreach (var file in AssetDatabase.GetAllAssetPaths ()) {
                if (helper.IsAssetBundle (file)) {
                    list.Add (file);
                }
            }
            return list.ToArray ();
        }
    }
}