﻿using UnityEngine;
using System;
using System.Collections.Generic;

using UObject = UnityEngine.Object;
using System.Collections;
using JxUnity.ResDB.Private;
using System.IO;
using System.Runtime.CompilerServices;

namespace JxUnity.ResDB
{
    internal sealed class ResDBPackageLoaderMono : MonoBehaviour, IResDBLoader
    {
        private static ResDBPackageLoaderMono instance;
        public static ResDBPackageLoaderMono Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject($"__m_{nameof(ResDBPackageLoaderMono)}");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<ResDBPackageLoaderMono>();
                }
                return instance;
            }
        }

        private AssetBundleManifest manifest = null;

        private AssetBundleMapping assetMapping = null;

        private Dictionary<string, AssetBundle> assetbundleCaching = null;

        private Dictionary<string, UObject> assetCaching = null;

        //加载依赖表和资源映射表
        private void Awake()
        {
            string manifestFilename = string.Format("{0}/{1}",
                ResDBConfig.WorkingPath, ResDBConfig.WorkingFolderName);

            AssetBundle manifestBundle = AssetBundle.LoadFromFile(manifestFilename);
            this.manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            manifestBundle.Unload(false);

            var mappingFile = AssetNameUtility.FormatBundleName(ResDBConfig.LoadMappingFile);
            AssetBundle mappingBundle = AssetBundle.LoadFromFile(mappingFile);

            string mapping = mappingBundle.LoadAsset<TextAsset>(ResDBConfig.MapName).text;
            this.assetMapping = new AssetBundleMapping(mapping);
            mappingBundle.Unload(true);

            this.assetbundleCaching = new Dictionary<string, AssetBundle>();
            this.assetCaching = new Dictionary<string, UObject>();
        }

        #region AssetBundle
        public bool IsLoadedBundle(string bundleName)
        {
            return this.assetbundleCaching.ContainsKey(bundleName);
        }

        public AssetBundle GetAssetBundleCache(string bundleName)
        {
            AssetBundle bundle = null;
            this.assetbundleCaching.TryGetValue(bundleName, out bundle);
            return bundle;
        }
        //TODO: 应该要去加载依赖
        public void LoadAssetBundle(string bundleName)
        {
            var fullName = $"{ResDBConfig.WorkingPath}/{bundleName}";
            if (this.IsLoadedBundle(bundleName))
            {
                return;
            }
            this.assetbundleCaching.Add(bundleName, AssetBundle.LoadFromFile(fullName));
        }

        private IEnumerator LoadAssetBundleCo(string bundleName, Action<AssetBundle> cb)
        {
            var req = AssetBundle.LoadFromFileAsync(bundleName);
            yield return req;
            this.assetbundleCaching.Add(bundleName, req.assetBundle);
            cb?.Invoke(req.assetBundle);
        }
        public void LoadAssetBundleAsync(string bundleName, Action<AssetBundle> cb)
        {
            bundleName = ResDBConfig.WorkingPath + "/" + bundleName;
            if (this.IsLoadedBundle(bundleName))
            {
                cb?.Invoke(this.GetAssetBundleCache(bundleName));
                return;
            }
            var req = AssetBundle.LoadFromFileAsync(bundleName);
            StartCoroutine(LoadAssetBundleCo(bundleName, cb));
        }
        public void LoadAllAssetBundles()
        {
            var abNames = this.manifest.GetAllAssetBundles();
            foreach (var item in abNames)
            {
                if (!this.IsLoadedBundle(item))
                {
                    this.LoadAssetBundle(item);
                }
            }
        }

        public void UnloadAssetBundle(string bundleName, bool force)
        {
            this.assetbundleCaching[bundleName].Unload(force);
            this.assetbundleCaching.Remove(bundleName);
        }
        internal void UnloadAllAssetBundle(bool v)
        {
            foreach (var item in this.assetbundleCaching)
            {
                item.Value.Unload(v);
            }
            this.assetbundleCaching.Clear();
        }
        #endregion

        #region Assets
        private IEnumerator LoadAssetCo(AssetBundle ab, string path, string name, Type type, Action<UObject> cb)
        {
            var req = ab.LoadAssetAsync(name, type);
            yield return req;
            this.assetCaching.Add(path, req.asset);
            cb?.Invoke(req.asset);
        }
        internal bool IsLoadedAsset(string path)
        {
            return this.assetCaching.ContainsKey(path);
        }
        internal void AddAssetCache(string path, UObject asset)
        {
            if (this.IsLoadedAsset(path))
            {
                return;
            }
            this.assetCaching.Add(path, asset);
        }
        internal UObject GetAssetCache(string path)
        {
            UObject obj = null;
            this.assetCaching.TryGetValue(path, out obj);
            return obj;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private UObject LoadAsset(string path, Type type)
        {
            path = path.ToLower();
            var item = this.assetMapping.Mapping(path);
            if (item == null)
            {
                return null;
            }

            if (this.IsLoadedAsset(path))
            {
                return this.GetAssetCache(path);
            }
            else
            {
                //TODO: 如果没有ab应该去加载ab，这里暂时直接取缓存
                this.LoadAssetBundle(item.assetPackageName);
                var ab = this.GetAssetBundleCache(item.assetPackageName);
                UObject asset = ab.LoadAsset(item.assetName, type);
                return asset;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LoadAssetAsync(string path, Type type, Action<UObject> assetCallBack)
        {
            path = path.ToLower();
            var item = this.assetMapping.Mapping(path);
            if (item == null)
            {
                throw new ArgumentException("asset not found");
            }

            if (this.IsLoadedAsset(path))
            {
                assetCallBack?.Invoke(this.GetAssetCache(path));
            }
            else
            {
                //TODO: 如果没有ab应该去加载ab，这里暂时直接取缓存
                var ab = this.GetAssetBundleCache(item.assetPackageName);
                StartCoroutine(LoadAssetCo(ab, path, item.assetName, type, assetCallBack));
            }
        }


        #endregion

        public UObject Load(string index, Type type)
        {
            return this.LoadAsset(index, type);
        }

        public void LoadAsync(string index, Type type, Action<UObject> cb)
        {
            this.LoadAssetAsync(index, type, cb);
        }
    }
}