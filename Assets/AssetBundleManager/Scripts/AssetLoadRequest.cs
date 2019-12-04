using System.Collections;
using System.IO;
using System.Linq;
using AssetBundleManager.Lib;
using UnityEngine;

namespace AssetBundleManager {
    public interface IAssetLoadRequest {
        string assetBundleName { get; }
        string assetName { get; }
        string subAssetName { get; }
        bool isDone { get; }
        float progress { get; }

        UnityEngine.Object GetAsset ();
        void SetRequest (AssetBundleLoadRequest request);
#if UNITY_EDITOR
        void LoadLocalAsset ();
#endif
        void Update ();
    }

    public class AssetLoadRequest<T> : IAssetLoadRequest where T : UnityEngine.Object {
        public string assetBundleName { get; protected set; }
        public string assetName { get; protected set; }
        public string subAssetName { get; protected set; }

        public bool isDone {
            get {
                return _state == State.Done;
            }
        }

        public float progress {
            get {
                switch (_state) {
                    case State.Idle:
                        return 0f;
                    case State.Done:
                        return 1f;
                }
                var p0 = _assetBundleLoadRequest != null ? _assetBundleLoadRequest.progress : 0f;
                var p1 = _assetBundleRequest != null ? _assetBundleRequest.progress : 0f;
                return (p0 + p1) / 2f;
            }
        }

        public enum State {
            Idle,
            WaitingDownload,
            WaitingLoadAsync,
            Done,
        }

        private static string[] kUnnecessaryLoadExtensions = {
            ".unity"
        };

        private State _state = State.Idle;
        private AssetBundleLoadRequest _assetBundleLoadRequest = null;
        private AssetBundleRequest _assetBundleRequest = null;
        private T _asset = null;

        public AssetLoadRequest (string assetBundleName, string assetName = null, string subAssetName = null) {
            this.assetBundleName = assetBundleName;
            this.assetName = assetName;
            this.subAssetName = subAssetName;
        }

        public UnityEngine.Object GetAsset () {
            return _asset;
        }

        public void SetRequest (AssetBundleLoadRequest request) {
            _assetBundleLoadRequest = request;
            _state = State.WaitingDownload;
        }

#if UNITY_EDITOR
        public void LoadLocalAsset () {
            if (!IsNecessaryLoadAsset ()) {
                _state = State.Done;
                return;
            }
            var paths = UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundle (assetBundleName);
            if (paths.Length == 0 || !paths.Contains (assetName)) {
                Debug.LogError ("There is no asset " + assetName + " in " + assetBundleName);
                _state = State.Done;
                return;
            }
            _asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T> (assetName);
            if (_asset == null) {
                Debug.LogError ("failed to load asset " + assetName + " in " + assetBundleName);
            }
            _state = State.Done;
        }
#endif

        public void Update () {
            switch (_state) {
                case State.Idle:
                case State.Done:
                    break;
                case State.WaitingDownload:
                    if (_assetBundleLoadRequest.isDone) {
                        if (IsNecessaryLoadAsset ()) {
                            if (string.IsNullOrEmpty (subAssetName)) {
                                _assetBundleRequest = _assetBundleLoadRequest.LoadAssetAsync<T> (assetName);
                            } else {
                                _assetBundleRequest = _assetBundleLoadRequest.LoadAssetWithSubAssetsAsync<T> (assetName);
                            }
                            _state = State.WaitingLoadAsync;
                        } else {
                            _state = State.Done;
                        }
                    }
                    break;
                case State.WaitingLoadAsync:
                    if (_assetBundleRequest.isDone) {
                        if (IsNecessaryLoadAsset ()) {
                            _asset = _assetBundleRequest.asset as T;
                        } else {
                            var assets = _assetBundleRequest.allAssets;
                            if (assets != null) {
                                foreach (var asset in assets) {
                                    if (asset.name.Equals (subAssetName)) {
                                        _asset = asset as T;
                                        break;
                                    }
                                }
                            }
                        }
                        if (_asset == null) {
                            Debug.LogError ("failed to load asset " + assetName + " in " + assetBundleName);
                        }
                        _state = State.Done;
                    }
                    break;
            }
        }

        private bool IsNecessaryLoadAsset () {
            if (!string.IsNullOrEmpty (assetName)) {
                foreach (var ex in kUnnecessaryLoadExtensions) {
                    if (Path.GetExtension (assetName).Equals (ex)) {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }
}