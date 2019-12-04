using System;
using System.Collections.Generic;
using System.IO;
using AssetBundleManager.Lib;
using UnityEngine;

namespace AssetBundleManager {
    public interface IAssetBundleHelper {
#if UNITY_EDITOR
        bool IsAutoNameAssetBundle ();
        UnityEditor.BuildTarget GetBuildTarget ();
        string GetBuildDestPath ();
        string GetBuildCollectionDestPath ();
#endif

        bool IsAssetBundle (string path);
        string GetAssetBundleName (string path);
        string GetUrl (string path);
        string GetManifestUrl ();
        string GetCollectionAssetPath ();
        string GetCollectionName ();
        string GetCollectionUrl ();
        string GetCollectionManifestUrl ();
    }
}