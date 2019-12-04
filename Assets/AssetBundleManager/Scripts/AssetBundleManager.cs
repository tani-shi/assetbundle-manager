using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AssetBundleManager.Lib;
using UnityEngine;
using UnityEngine.Networking;

namespace AssetBundleManager {
    public class AssetBundleManager : SingletonMonoBehaviour<AssetBundleManager> {
        public static long LoadRequestMaxBytes {
            get {
                return Instance != null ? Instance._loadRequestMaxBytes : 0L;
            }
        }

        public static int LoadRequestMaxCount {
            get {
                return Instance != null ? Instance._loadRequestMaxCount : 0;
            }
        }

        public static float TimeoutSeconds {
            get {
                return Instance != null ? Instance._timeoutSeconds : 0f;
            }
        }

        public static int TimeoutRetryLimit {
            get {
                return Instance != null ? Instance._timeoutRetryLimit : 0;
            }
        }

        public static bool IsDownloading {
            get {
                if (Instance != null) {
                    return Instance._loadingRequests.Count > 0 ||
                        Instance._downloadingRequests.Count > 0 ||
                        Instance._requestQueue.Count > 0;
                }
                return false;
            }
        }

        public static bool IsLoading {
            get {
                if (Instance != null) {
                    return Instance._assetLoadingRequests.Count > 0;
                }
                return false;
            }
        }

        public static bool HasError {
            get {
                if (Instance != null) {
                    return Instance._errorQueue.Count > 0;
                }
                return false;
            }
        }

        public static ulong AllDownloadingSize {
            get {
                if (Instance == null) {
                    return 0L;
                }

                ulong total = 0L;
                foreach (var request in Instance._downloadingRequests) {
                    total += request.size;
                }
                foreach (var request in Instance._loadingRequests) {
                    total += request.size;
                }
                foreach (var request in Instance._requestQueue) {
                    total += request.size;
                }
                return total;
            }
        }

        public static int RequestCount {
            get {
                if (Instance == null) {
                    return 0;
                }
                return Instance._loadingRequests.Count + Instance._requestQueue.Count + Instance._errorQueue.Count;
            }
        }

        public static float Progress {
            get {
                var total = 0f;
                foreach (var request in Instance._loadingRequests) {
                    total += request.progress;
                }
                return total / (float) RequestCount;
            }
        }

        public static bool IsReady {
            get {
                return Instance != null & Instance.ready;
            }
        }

        public bool ready { get; private set; }

        [SerializeField]
        private bool _isUseLocalResources = false;
        [SerializeField]
        private int _timeoutRetryLimit = 3;
        [SerializeField]
        private float _timeoutSeconds = 20.0f;
        [SerializeField]
        private int _loadRequestMaxCount = 35;
        [SerializeField]
        private long _loadRequestMaxBytes = 10L * 1024L * 1024L; // 10MBytes

        private Action<AssetBundleLoadRequest> _onError = null;
        private Queue<AssetBundleLoadRequest> _requestQueue = new Queue<AssetBundleLoadRequest> ();
        private Queue<AssetBundleLoadRequest> _errorQueue = new Queue<AssetBundleLoadRequest> ();
        private HashSet<AssetBundleLoadRequest> _loadingRequests = new HashSet<AssetBundleLoadRequest> ();
        private HashSet<AssetBundleLoadRequest> _downloadingRequests = new HashSet<AssetBundleLoadRequest> ();
        private Dictionary<string, AssetBundleLoadRequest> _requestMap = new Dictionary<string, AssetBundleLoadRequest> ();
        private HashSet<IAssetLoadRequest> _assetLoadingRequests = new HashSet<IAssetLoadRequest> ();
        private Dictionary<string, AssetBundleInfoCollection.Info> _infoDictionary = new Dictionary<string, AssetBundleInfoCollection.Info> ();
        private AssetBundleManifest _manifest = null;
        private AssetBundleInfoCollection _infoCollection = null;
        private Dictionary<string, UnityEngine.Object> _loadedAssets = new Dictionary<string, UnityEngine.Object> ();
        private IAssetBundleHelper _helper = null;

        public static T GetAsset<T> (string assetName) where T : UnityEngine.Object {
            T asset = null;
            if (Instance._helper.IsAssetBundle (assetName) && Instance._loadedAssets.ContainsKey (assetName)) {
                asset = Instance._loadedAssets[assetName] as T;
            }
            if (asset == null) {
#if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlaying) {
                    asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T> (assetName);
                } else
#endif
                {
                    if (!string.IsNullOrEmpty (Path.GetExtension (assetName))) {
                        asset = Resources.Load<T> (assetName.Replace ("Assets/App/AssetBundles/", "").Replace (Path.GetExtension (assetName), ""));
                    } else {
                        asset = Resources.Load<T> (assetName.Replace ("Assets/App/AssetBundles/", ""));
                    }
                }
            }
            if (asset == null) {
                Debug.LogError ("Failed to get an asset. " + assetName);
            }
            return asset;
        }

        public static void AddRequest (string assetName) {
            if (!IsReady) {
                Debug.LogWarning ("AssetBundleManager was not initialized.");
                return;
            }
            AddRequest<UnityEngine.Object> (assetName);
        }

        public static void AddDownloadRequest (string assetBundleName) {
            if (!IsReady) {
                Debug.LogWarning ("AssetBundleManager was not initialized.");
                return;
            }
            AddRequest<UnityEngine.Object> (null, assetBundleName);
        }

        public static void AddRequests (string[] assetNames) {
            if (!IsReady) {
                Debug.LogWarning ("AssetBundleManager was not initialized.");
                return;
            }
            foreach (var assetName in assetNames) {
                AddRequest (assetName);
            }
        }

        public static void AddDownloadRequests (string[] assetBundleNames) {
            if (!IsReady) {
                Debug.LogWarning ("AssetBundleManager was not initialized.");
                return;
            }
            foreach (var assetBundleName in assetBundleNames) {
                AddDownloadRequest (assetBundleName);
            }
        }

        public static void AddRequest<T> (string assetName, string assetBundleName = null, string subAssetName = null) where T : UnityEngine.Object {
            if (!IsReady) {
                Debug.LogWarning ("AssetBundleManager was not initialized.");
                return;
            }
            if (string.IsNullOrEmpty (assetBundleName) && !string.IsNullOrEmpty (assetName)) {
                assetBundleName = Instance._helper.GetAssetBundleName (assetName);
            }
            if (string.IsNullOrEmpty (assetBundleName)) {
                Debug.LogWarning ("Attempted to load an asset that isn't used to assetbundle. " + assetName);
                return;
            }
            if (!string.IsNullOrEmpty (assetName) && IsAssetLoaded (assetName)) {
                Debug.Log (assetName + " has already loaded.");
                return;
            }
            Instance.AddRequestInternal (new AssetLoadRequest<T> (assetBundleName, assetName, subAssetName));
        }

        public static void Retry () {
            if (!IsReady) {
                return;
            }
            while (Instance._errorQueue.Count > 0) {
                var request = Instance._errorQueue.Dequeue ();
                if (!Instance._loadingRequests.Contains (request) &&
                    !Instance._downloadingRequests.Contains (request)) {
                    request.Dispose ();
                    request.Load ();
                    Instance.AddLoadingRequest (request);
                }
            }
        }

        public static void RemoveAllRequests () {
            if (!IsReady) {
                return;
            }

            Instance._assetLoadingRequests.Clear ();
            Instance._requestQueue.Clear ();
            Instance._errorQueue.Clear ();
            Instance._loadingRequests.Clear ();
            Instance._requestQueue.Clear ();

            foreach (var kv in Instance._requestMap) {
                kv.Value.Dispose ();
            }
            Instance._requestMap.Clear ();
        }

        public static void RemoveRequest (IAssetLoadRequest request) {
            if (!IsReady || !Instance._assetLoadingRequests.Contains (request)) {
                return;
            }
            Instance._assetLoadingRequests.Remove (request);
            Instance.RemoveRequestInternalWithDependencies (request.assetBundleName);
        }

        public static bool IsAssetLoaded (string assetName) {
            return Instance._loadedAssets.ContainsKey (assetName);
        }

        public static bool IsAssetCached (string assetName) {
            var assetBundleName = Instance._helper.GetAssetBundleName (assetName);
            if (string.IsNullOrEmpty (assetBundleName)) {
                return false;
            }
            return IsAssetBundleCached (Instance._helper.GetAssetBundleName (assetName), Instance._manifest.GetAssetBundleHash (assetBundleName));
        }

        public static bool IsAssetBundleCached (string assetBundleName, Hash128 hash) {
            if (IsReady) {
                return Caching.IsVersionCached (Instance._helper.GetUrl (assetBundleName), hash);
            }
            return false;
        }

        public static bool IsAssetBundleCachedByFullPath (string assetBundlePath, Hash128 hash) {
            if (IsReady) {
                return Caching.IsVersionCached (assetBundlePath, hash);
            }
            return false;
        }

        public void SetErrorCallback (Action<AssetBundleLoadRequest> callback) {
            _onError = callback;
        }

        public void Initialize (IAssetBundleHelper helper) {
            _helper = helper;
        }

        private IEnumerator LoadManifest () {
            if (_isUseLocalResources) {
                Debug.Log ("AssetBundleManager has ready to use by using local resources.");
                ready = true;
                yield break;
            }

            while (!Caching.ready) {
                yield return null;
            }

            yield return LoadAssetBundleManifest (_helper.GetManifestUrl (), (o) => _manifest = o);
            if (_manifest == null) {
                Debug.LogError ("Failed to load asset bundle manifest.");
                yield break;
            }

            yield return LoadAssetBundleInfo ();
            if (_infoCollection == null) {
                Debug.LogError ("Failed to load asset bundle info.");
                yield break;
            }

            foreach (var info in _infoCollection.list) {
                _infoDictionary[info.assetBundleName] = info;
            }

            Debug.Log ("AssetBundleManager has ready to use.");
            ready = true;
        }

        public void OnUpdate () {
            if (!IsReady) {
                return;
            }
            UpdateAssetBundleLoadRequestQueue ();
            UpdateAssetBundleLoadRequest ();
            UpdateAssetLoadRequest ();
        }

        private void UpdateAssetBundleLoadRequestQueue () {
            while (_requestQueue.Count > 0) {
                if (_downloadingRequests.Count + _loadingRequests.Count >= _loadRequestMaxCount) {
                    break;
                }
                var request = _requestQueue.Dequeue ();
                request.Load ();
                AddLoadingRequest (request);
            }
        }

        private void UpdateAssetBundleLoadRequest () {
            var rm = new HashSet<AssetBundleLoadRequest> ();
            var requests = new List<AssetBundleLoadRequest> ();
            requests.AddRange (_downloadingRequests);
            requests.AddRange (_loadingRequests);
            foreach (var request in requests) {
                request.Update ();
                if (request.isDone) {
                    Debug.Log ("Cached an assetbundle. " + request.assetBundleName);
                    rm.Add (request);
                } else if (request.isError) {
                    rm.Add (request);
                    _errorQueue.Enqueue (request);
                    if (_onError != null) {
                        _onError (request);
                    }
                }
            }
            foreach (var request in rm) {
                RemoveLoadingRequest (request);
            }
        }

        private void UpdateAssetLoadRequest () {
            var rm = new HashSet<IAssetLoadRequest> ();
            foreach (var request in _assetLoadingRequests) {
                request.Update ();
                if (request.isDone) {
                    var asset = request.GetAsset ();
                    if (asset != null) {
                        Debug.Log ("Loaded an asset. " + request.assetName);
                        _loadedAssets.Add (request.assetName, asset);
                    }
                    rm.Add (request);
                }
            }
            foreach (var request in rm) {
                _assetLoadingRequests.Remove (request);
            }
        }

        private IEnumerator LoadAssetBundleManifest (string uri, Action<AssetBundleManifest> action) {
            if (_isUseLocalResources) {
                yield break;
            }

            Debug.Log ("Loading manifest. " + uri);
            var startTime = Time.time;
            using (var www = UnityWebRequestAssetBundle.GetAssetBundle (uri)) {
                www.SendWebRequest ();
                while (!www.isDone && Time.time - startTime < _timeoutSeconds) {
                    yield return null;
                }
                if (www.isDone && !string.IsNullOrEmpty (www.error)) {
                    Debug.LogError (www.error);
                    yield break;
                } else if (!www.isDone) {
                    Debug.LogError ("Loading asset bundle manifest is timeout.");
                    yield break;
                }

                var assetBundle = DownloadHandlerAssetBundle.GetContent (www);
                var request = assetBundle.LoadAssetAsync<AssetBundleManifest> ("AssetBundleManifest");
                while (!request.isDone) {
                    yield return null;
                }
                action (request.asset as AssetBundleManifest);
                assetBundle.Unload (false);
            }
        }

        private IEnumerator LoadAssetBundleInfo () {
            if (_isUseLocalResources) {
                yield break;
            }

            AssetBundleManifest manifest = null;
            yield return LoadAssetBundleManifest (_helper.GetCollectionManifestUrl (), (o) => manifest = o);
            if (manifest == null) {
                yield break;
            }
            var startTime = Time.time;
            var uri = _helper.GetCollectionUrl ();
            var hash = manifest.GetAssetBundleHash (_helper.GetCollectionName ());
            using (var www = UnityWebRequestAssetBundle.GetAssetBundle (uri, hash)) {
                www.SendWebRequest ();
                while (!www.isDone && Time.time - startTime < _timeoutSeconds) {
                    yield return null;
                }
                if (www.isDone && !string.IsNullOrEmpty (www.error)) {
                    Debug.LogError (www.error);
                    yield break;
                } else if (!www.isDone) {
                    Debug.LogError ("Loading asset bundle info is timeout.");
                    yield break;
                }

                var path = _helper.GetCollectionAssetPath ();
                var assetBundle = DownloadHandlerAssetBundle.GetContent (www);
                var request = assetBundle.LoadAssetAsync<AssetBundleInfoCollection> (path);
                while (!request.isDone) {
                    yield return null;
                }
                _infoCollection = request.asset as AssetBundleInfoCollection;
                assetBundle.Unload (false);
            }
        }

        private void AddRequestInternal (IAssetLoadRequest request) {
            _assetLoadingRequests.Add (request);
            if (_isUseLocalResources) {
                request.LoadLocalAsset ();
            } else {
                request.SetRequest (AddRequestInternalWithDependencies (request.assetBundleName));
            }
        }

        private AssetBundleLoadRequest AddRequestInternalWithDependencies (string assetBundleName) {
            Debug.Log ("Start loading an assetbundle. " + assetBundleName);
            var dependencies = _infoDictionary[assetBundleName].manifestInfo.dependencies;
            var requests = new List<AssetBundleLoadRequest> ();
            foreach (var d in dependencies) {
                requests.Add (AddRequestInternalWithDependencies (d));
            }
            var request = AddRequestInternal (assetBundleName);
            request.SetDependencies (requests);
            return request;
        }

        private AssetBundleLoadRequest AddRequestInternal (string assetBundleName) {
            AssetBundleLoadRequest request;
            if (_requestMap.TryGetValue (assetBundleName, out request)) {
                request.referencedCount++;
            } else {
                request = new AssetBundleLoadRequest (_helper, _infoDictionary[assetBundleName]);
                _requestMap[request.assetBundleName] = request;
                _requestQueue.Enqueue (request);
            }
            return request;
        }

        private void RemoveRequestInternalWithDependencies (string assetBundleName) {
            var dependencies = _infoDictionary[assetBundleName].manifestInfo.dependencies;
            foreach (var d in dependencies) {
                RemoveRequestInternalWithDependencies (d);
            }
            RemoveRequestInternal (assetBundleName);
        }

        private void RemoveRequestInternal (string assetBundleName) {
            AssetBundleLoadRequest request;
            if (_requestMap.TryGetValue (assetBundleName, out request)) {
                if (request.referencedCount <= 0) {
                    if (_requestQueue.Contains (request)) {
                        for (int i = 0; i < _requestQueue.Count; i++) {
                            var item = _requestQueue.Dequeue ();
                            if (item != request) {
                                _requestQueue.Enqueue (item);
                            }
                        }
                    }
                    RemoveLoadingRequest (request);
                    _requestMap.Remove (assetBundleName);
                    request.Dispose ();
                } else {
                    request.referencedCount--;
                }
            }
        }

        private void AddLoadingRequest (AssetBundleLoadRequest request) {
            if (!_loadingRequests.Contains (request) && !_downloadingRequests.Contains (request)) {
                if (IsAssetBundleCachedByFullPath (request.assetBundleName, request.hash)) {
                    _loadingRequests.Add (request);
                } else {
                    _downloadingRequests.Add (request);
                }
            }
        }

        private void RemoveLoadingRequest (AssetBundleLoadRequest request) {
            if (_downloadingRequests.Contains (request)) {
                _downloadingRequests.Remove (request);
            }
            if (_loadingRequests.Contains (request)) {
                _loadingRequests.Remove (request);
            }
        }

        private uint GetCrc (string assetBundleName) {
            AssetBundleInfoCollection.Info info;
            if (_infoDictionary.TryGetValue (assetBundleName, out info)) {
                return info.manifestInfo.crc;
            }
            return 0;
        }

        private ulong GetSize (string assetBundleName) {
            AssetBundleInfoCollection.Info info;
            if (_infoDictionary.TryGetValue (assetBundleName, out info)) {
                return info.size;
            }
            return 0L;
        }
    }
}