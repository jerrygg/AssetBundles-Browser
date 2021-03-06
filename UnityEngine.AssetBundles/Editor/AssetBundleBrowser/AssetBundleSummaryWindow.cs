﻿using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System.IO;
using System;

namespace UnityEngine.AssetBundles
{
	internal class AssetBundleSummaryWindow : EditorWindow
	{
        [SerializeField]
        string m_bundlePath = string.Empty;
        [SerializeField]
        TreeViewState m_treeState;
        public Editor m_editor;
        BundleTree m_tree;
        DateTime m_lastUpdate;
        Rect m_horizontalSplitterRect;
        const float m_splitterWidth = 3;
        bool m_resizingHorizontalSplitter = false;
        static Dictionary<string, AssetBundleCreateRequest> m_bundles = new Dictionary<string, AssetBundleCreateRequest>();



        [MenuItem("AssetBundles/Inspect", priority = 3)]
        static void ShowWindow()
        {
            GetWindow<AssetBundleSummaryWindow>().titleContent = new GUIContent("ABInspect");
        }

        internal static void ShowWindow(string bundlePath)
		{
            var window = GetWindow<AssetBundleSummaryWindow>();
            window.titleContent = new GUIContent("ABInspect");
            window.Init(bundlePath);
		}

        private void Init(string bundlePath)
        {
            m_bundlePath = bundlePath;
        }

        class BundleTree : TreeView
        {
            AssetBundleSummaryWindow window;
            public BundleTree(AssetBundleSummaryWindow w, TreeViewState s) : base(s)
            {
                window = w;
                showBorder = true;
            }

            public bool Update()
            {
                bool updating = false;
                foreach (var i in GetRows())
                {
                    var ri = i as Item;
                    if (ri != null)
                    {
                        if (ri.Update())
                            updating = true;
                    }
                }
                return updating;
            }

            class Item : TreeViewItem
            {
                string m_bundlePath;
                AssetBundleCreateRequest m_bundleRequest { get { return m_bundles[m_bundlePath]; } }
                int m_prevPercent = -1;
                bool m_loading = true;
                public Editor editor
                {
                    get
                    {
                        return (m_bundleRequest == null || m_bundleRequest.assetBundle == null) ? null : Editor.CreateEditor(m_bundleRequest.assetBundle);
                    }
                }

                public Item(string path) : base(path.GetHashCode(), 0, Path.GetFileName(path))
                {
                    m_bundlePath = path;
                }

                public bool Update()
                {
                    if (!m_loading)
                        return false;
                    if (m_bundleRequest.isDone)
                    {
                        displayName = Path.GetFileName(m_bundlePath);
                        m_loading = false;
                        return true;
                    }
                    else
                    {
                        int per = (int)(m_bundleRequest.progress * 100);
                        if (per != m_prevPercent)
                        {
                            displayName = Path.GetFileName(m_bundlePath) + " " + (m_prevPercent = per) + "%";
                            return true;
                        }
                    }
                    return false;
                }
            }

            protected override void SelectionChanged(IList<int> selectedIds)
            {
                window.m_editor = Utilities.FindItem<Item>(rootItem, selectedIds[0]).editor;
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem(-1, -1);
                root.children = new List<TreeViewItem>();
                foreach (var b in m_bundles)
                    root.AddChild(new Item(b.Key));
                return root;
            }
        }

        private void Update()
        {
            if (m_tree == null && Directory.Exists(m_bundlePath))
            {
                foreach (var fn in Directory.GetFiles(m_bundlePath))
                {
                    if (Path.GetExtension(fn) == ".manifest")
                    {
                        var f = fn.Substring(0, fn.LastIndexOf('.')).Replace('\\', '/');
                        AssetBundleCreateRequest req;
                        if (m_bundles.TryGetValue(f, out req))
                        {
                            if (req.isDone && req.assetBundle != null)
                            {
                                req.assetBundle.Unload(true);
                                m_bundles.Remove(f);
                            }
                        }
                        if (!m_bundles.ContainsKey(f))
                            m_bundles.Add(f, AssetBundle.LoadFromFileAsync(f));
                    }
                }

                if (m_treeState == null)
                    m_treeState = new TreeViewState();
                m_tree = new BundleTree(this, m_treeState);
                m_tree.Reload();
            }

            if (m_tree == null)
                return;


            if (m_resizingHorizontalSplitter)
                Repaint();

            if ((DateTime.Now - m_lastUpdate).TotalSeconds > .5f)
            {
                if (m_tree.Update())
                {
                    m_tree.SetSelection(m_tree.GetSelection());
                    Repaint();
                }
                m_lastUpdate = DateTime.Now;
            }
        }

        void OnGUI()
		{
            GUILayout.BeginHorizontal();
            var f = GUILayout.TextField(m_bundlePath, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Browse"))
                f = EditorUtility.OpenFolderPanel("Bundle Folder", f, string.Empty);
            if (f != m_bundlePath)
                Init(f);
            GUILayout.EndHorizontal();

            if (m_tree == null)
                return;

            float h = 21 + m_splitterWidth;
            HandleHorizontalResize(h);
            m_tree.OnGUI(new Rect(m_splitterWidth, h, m_horizontalSplitterRect.x - m_splitterWidth * 2, position.height - (h + m_splitterWidth)));
            if (m_editor != null)
            {
                GUILayout.BeginArea(new Rect(m_horizontalSplitterRect.x + m_splitterWidth, h, position.width - (m_horizontalSplitterRect.x + m_splitterWidth), position.height - (h + m_splitterWidth)));
                m_editor.Repaint();
                m_editor.OnInspectorGUI();
                GUILayout.EndArea();
            }
        }

        void OnEnable()
        {
            m_horizontalSplitterRect = new Rect(position.width / 2, 0, m_splitterWidth, this.position.height);
        }

        private void HandleHorizontalResize(float h)
        {
            m_horizontalSplitterRect.x = Mathf.Clamp(m_horizontalSplitterRect.x, position.width * .1f, (position.width - m_splitterWidth) * .9f);
            m_horizontalSplitterRect.y = h;
            m_horizontalSplitterRect.height = position.height - (h + m_splitterWidth);

            EditorGUIUtility.AddCursorRect(m_horizontalSplitterRect, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.mouseDown && m_horizontalSplitterRect.Contains(Event.current.mousePosition))
                m_resizingHorizontalSplitter = true;

            if (m_resizingHorizontalSplitter)
                m_horizontalSplitterRect.x = Mathf.Clamp(Event.current.mousePosition.x, position.width * .1f, (position.width - m_splitterWidth) * .9f);

            if (Event.current.type == EventType.MouseUp)
                m_resizingHorizontalSplitter = false;
        }

    }
}
