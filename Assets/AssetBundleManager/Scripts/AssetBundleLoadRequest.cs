using System.Collections;
using System.Collections.Generic;
using AssetBundleManager.Lib;
using UnityEngine;
using UnityEngine.Networking;

namespace AssetBundleManager {
    public class AssetBundleLoadRequest : System.IDisposable {
        public enum State {
            Idle,
            WaitingDependencies,
            WaitingRequestWWW,
            LoadingWWW,
            Done,
            Error,
        }

        public bool isError {
            get {
                return (_state == State.Error);
            }
        }

        public bool isDone {
            get {
                return (_state == State.Done);
            }
        }

        public float progress {
            get {
                switch (_state) {
                    case State.Done:
                        return 1f;
                    case State.Idle:
                    case State.WaitingDependencies:
                        return 0f;
                    default:
                        return _progress;
                }
            }
        }

        public int referencedCount { get; set; }
        public string assetBundleName { get; protected set; }
        public Hash128 hash { get; protected set; }
        public ulong size { get; protected set; }
        public uint crc { get; protected set; }
        public string error { get; protected set; }

        private State _state = State.Idle;
        private UnityWebRequest _www = null;
        private float _progress = 0.0f;
        private List<AssetBundleLoadRequest> _dependencies = null;
        private AssetBundle _assetBundle = null;
        private float _requestedTime = 0.0f;
        private int _retriedCount = 0;
        private IAssetBundleHelper _helper;

        public AssetBundleLoadRequest (IAssetBundleHelper helper, AssetBundleInfoCollection.Info info) {
            assetBundleName = info.assetBundleName;
            hash = info.manifestInfo.assetFileHash;
            size = info.size;
            crc = info.manifestInfo.crc;
            error = null;
            _helper = helper;
        }

        public void SetDependencies (List<AssetBundleLoadRequest> dependencies) {
            _dependencies = dependencies;
        }

        public void Load () {
            if (_state != State.Idle) {
                return;
            }
            if (_dependencies != null && _dependencies.Count > 0) {
                _state = State.WaitingDependencies;
            } else {
                _state = State.WaitingRequestWWW;
            }
        }

        private void Retry () {
            _requestedTime = Time.time;
            _progress = 0f;
            error = null;
            if (_progress > 0f && _www != null && !_www.isDone) {
                _state = State.LoadingWWW;
            } else {
                _state = State.WaitingRequestWWW;
                if (_www != null) {
                    _www.Dispose ();
                    _www = null;
                }
            }
            _retriedCount++;
        }

        public void Update () {
            switch (_state) {
                case State.WaitingDependencies:
                    _dependencies.ForEach ((obj) => {
                        if (!obj.isDone) return;
                    });
                    _state = State.WaitingRequestWWW;
                    break;
                case State.WaitingRequestWWW:
                    _www = UnityWebRequestAssetBundle.GetAssetBundle (_helper.GetUrl (assetBundleName), hash, crc);
                    _www.SendWebRequest ();
                    _requestedTime = Time.time;
                    _state = State.LoadingWWW;
                    break;
                case State.LoadingWWW:
                    if (_www == null) break;
                    if (_progress < _www.downloadProgress) {
                        _requestedTime = Time.time;
                        _progress = _www.downloadProgress;
                    }
                    if (_www.isDone) {
                        if (string.IsNullOrEmpty (_www.error)) {
                            _assetBundle = DownloadHandlerAssetBundle.GetContent (_www);
                            _state = State.Done;
                        } else {
                            if (_retriedCount >= AssetBundleManager.TimeoutRetryLimit) {
                                error = _www.error;
                                _state = State.Error;
                            } else {
                                Retry ();
                            }
                        }
                    } else if (Time.time - _requestedTime >= AssetBundleManager.TimeoutSeconds) {
                        if (_retriedCount >= AssetBundleManager.TimeoutRetryLimit) {
                            error = "timeout:" + assetBundleName;
                            _state = State.Error;
                            _www.Dispose ();
                            _www = null;
                        } else {
                            Retry ();
                        }
                    }
                    break;
            }
        }

        public void Dispose () {
            if (_assetBundle != null) {
                _assetBundle.Unload (false);
            }
            if (_www != null) {
                if (_www.isDone && _assetBundle != null) {
                    _assetBundle.Unload (false);
                }
                _www.Dispose ();
                _www = null;
            }
            _assetBundle = null;
            _state = State.Idle;
            _retriedCount = 0;
            error = null;
        }

        public T LoadAsset<T> (string assetName) where T : UnityEngine.Object {
            if (_assetBundle != null) {
                return _assetBundle.LoadAsset<T> (assetName);
            } else {
                Debug.LogWarning ("attempted to load asset without assetbundle. " + assetBundleName);
                return null;
            }
        }

        public T[] LoadAssetWithSubAssets<T> (string assetName) where T : UnityEngine.Object {
            if (_assetBundle != null) {
                return _assetBundle.LoadAssetWithSubAssets<T> (assetName);
            } else {
                Debug.LogWarning ("attempted to load asset without assetbundle. " + assetBundleName);
                return null;
            }
        }

        public AssetBundleRequest LoadAssetAsync<T> (string assetName) where T : UnityEngine.Object {
            if (_assetBundle != null) {
                return _assetBundle.LoadAssetAsync<T> (assetName);
            } else {
                Debug.LogWarning ("attempted to load asset without assetbundle. " + assetBundleName);
                return null;
            }
        }

        public AssetBundleRequest LoadAssetWithSubAssetsAsync<T> (string assetName) where T : UnityEngine.Object {
            if (_assetBundle != null) {
                return _assetBundle.LoadAssetWithSubAssetsAsync<T> (assetName);
            } else {
                Debug.LogWarning ("attempted to load asset without assetbundle. " + assetBundleName);
                return null;
            }
        }
    }
}