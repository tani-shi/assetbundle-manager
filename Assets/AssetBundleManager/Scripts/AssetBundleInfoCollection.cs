using System;
using System.Collections;
using System.Collections.Generic;
using AssetBundleManager.Lib;
using UnityEngine;

namespace AssetBundleManager {
    public class AssetBundleInfoCollection : ScriptableObject {
        [SerializableAttribute]
        public class Info {
            public string assetBundleName;
            public ManifestInfo manifestInfo;
            public ulong size;
        }

        public List<Info> list = new List<Info> ();
    }
}