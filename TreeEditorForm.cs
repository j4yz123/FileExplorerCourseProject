using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace FileTreeEditor
{
    public partial class TreeEditorForm : Form
    {
        private string currentFilePath;
        private readonly Dictionary<string, FileSystemWatcher> watchers = new Dictionary<string, FileSystemWatcher>();
        private bool showHidden = false;

        public TreeEditorForm()
        {
            InitializeComponent();
            contextMenuStrip1.Items.Clear();
            contextMenuStrip1.Items.Add("Обновить");
            contextMenuStrip1.Items.Add("Запустить");
            contextMenuStrip1.Items.Add("Копировать");
            contextMenuStrip1.Items.Add("Вставить");
            contextMenuStrip1.Items.Add("Удалить");


            var settings = new ToolStripMenuItem("Настройки");
            var toggleHidden = new ToolStripMenuItem("Показывать скрытые файлы") { CheckOnClick = true };
            toggleHidden.CheckedChanged += (s, e) => { showHidden = toggleHidden.Checked; RefreshAllNodes(); };
            settings.DropDownItems.Add(toggleHidden);
            contextMenuStrip1.Items.Add(new ToolStripSeparator());
            contextMenuStrip1.Items.Add(settings);

            treeView.NodeMouseClick += TreeView_NodeMouseClick;
            treeView.BeforeExpand += TreeView_BeforeExpand;

            contextMenuStrip1.ItemClicked += ContextMenu_ItemClicked;

            LoadLogicalDrives();
        }

        private void RefreshAllNodes()
        {
            foreach (TreeNode root in treeView.Nodes)
                if (root.IsExpanded)
                    LoadDirectory(root);
        }

        private void ContextMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var node = treeView.SelectedNode;
            switch (e.ClickedItem.Text)
            {
                case "Обновить": if (IsDirectoryNode(node)) LoadDirectory(node); break;
                case "Запустить": RunNode(node); break;
                case "Копировать": CopyToClipboard(node); break;
                case "Вставить": PasteFromClipboard(node); break;
                case "Удалить": DeleteNode(node); break;
            }
        }

        private bool IsDirectoryNode(TreeNode node)
        {
            var path = node?.Tag as string;
            return path != null && Directory.Exists(path);
        }

        private void TreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            treeView.SelectedNode = e.Node;
            if (e.Button == MouseButtons.Right)
            {
                var item = contextMenuStrip1.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Text == "Обновить");
                if (item != null)
                    item.Visible = IsDirectoryNode(e.Node);
                contextMenuStrip1.Show(treeView, e.Location);
            }
        }

        private void TreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            if (node.Nodes.Count == 1 && node.Nodes[0].Text == "...")
                LoadDirectory(node);
            WatchPath(node.Tag as string, node);
        }

        private void LoadLogicalDrives()
        {
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
                treeView.Nodes.Add(CreateDirectoryNode(d.RootDirectory.FullName));
        }

        private TreeNode CreateDirectoryNode(string path)
        {
            var trimmed = path.TrimEnd(Path.DirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            string display = string.IsNullOrEmpty(name) ? path : name;

            var node = new TreeNode(display) { Tag = path };
            node.Nodes.Add("...");
            return node;
        }

        public void LoadDirectory(TreeNode parentNode)
        {
            var path = parentNode.Tag as string;
            parentNode.Nodes.Clear();

            if (!Directory.Exists(path))
            {
                parentNode.Remove();
                return;
            }

            try
            {
                var dirs = Directory.GetDirectories(path)
                    .Where(d => showHidden || (new DirectoryInfo(d).Attributes & FileAttributes.Hidden) == 0);
                foreach (var dir in dirs)
                    parentNode.Nodes.Add(CreateDirectoryNode(dir));

                var files = Directory.GetFiles(path)
                    .Where(f => showHidden || (new FileInfo(f).Attributes & FileAttributes.Hidden) == 0);
                foreach (var file in files)
                    parentNode.Nodes.Add(new TreeNode(Path.GetFileName(file)) { Tag = file });
            }
            catch { }
        }

        private void WatchPath(string path, TreeNode node)
        {
            if (string.IsNullOrEmpty(path) || watchers.ContainsKey(path)) return;
            try
            {
                var watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                };
                watcher.Created += (s, e) => RefreshNode(path, node);
                watcher.Deleted += (s, e) => RefreshNode(path, node);
                watcher.Renamed += (s, e) => RefreshNode(path, node);
                watcher.EnableRaisingEvents = true;
                watchers[path] = watcher;
            }
            catch { }
        }

        private void RefreshNode(string path, TreeNode node)
        {
            if (node.Tag as string != path) return;
            if (node.TreeView.InvokeRequired)
                node.TreeView.BeginInvoke(new Action(() => LoadDirectory(node)));
            else
                LoadDirectory(node);
        }

        private void CopyToClipboard(TreeNode node)
        {
            var path = node.Tag as string;
            if (File.Exists(path) || Directory.Exists(path))
            {
                var list = new StringCollection { path };
                Clipboard.Clear();
                Clipboard.SetFileDropList(list);
            }
        }

        private void PasteFromClipboard(TreeNode node)
        {
            var targetDir = node.Tag as string;
            if (!Directory.Exists(targetDir)) return;
            if (Clipboard.ContainsFileDropList())
            {
                var list = Clipboard.GetFileDropList();
                foreach (string src in list)
                {
                    var name = Path.GetFileName(src);
                    var dst = Path.Combine(targetDir, name);
                    if (Directory.Exists(src))
                        CopyDirectory(src, dst);
                    else if (File.Exists(src))
                        File.Copy(src, dst, true);
                }
                LoadDirectory(node);
            }
        }

        private void RunNode(TreeNode node)
        {
            var path = node.Tag as string;
            if (File.Exists(path) && Path.GetExtension(path).ToLower() == ".exe")
                Process.Start(path);
        }

        private void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src))
                CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        private void DeleteNode(TreeNode node)
        {
            var path = node.Tag as string;
            if (Directory.Exists(path)) Directory.Delete(path, true);
            else if (File.Exists(path)) File.Delete(path);
            node.Remove();
        }

        public void SaveTreeToFile()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                using (var dlg = new SaveFileDialog { Filter = "Tree|*.treexml" })
                    if (dlg.ShowDialog() == DialogResult.OK)
                        currentFilePath = dlg.FileName;
                    else
                        return;
            }
            var doc = new XmlDocument();
            var root = doc.CreateElement("Tree");
            foreach (TreeNode node in treeView.Nodes)
                root.AppendChild(SerializeNode(doc, node));
            doc.AppendChild(root);
            doc.Save(currentFilePath);
            Text = Path.GetFileName(currentFilePath);
        }

        public void LoadTreeFromFile(string path)
        {
            currentFilePath = path;
            var doc = new XmlDocument();
            doc.Load(path);
            treeView.Nodes.Clear();
            foreach (XmlNode xnode in doc.DocumentElement.ChildNodes)
                treeView.Nodes.Add(DeserializeNode(xnode));
            Text = Path.GetFileName(path);
        }

        private XmlElement SerializeNode(XmlDocument doc, TreeNode node)
        {
            var el = doc.CreateElement("Node");
            el.SetAttribute("Text", node.Text);
            if (node.Tag is string tag) el.SetAttribute("Path", tag);
            foreach (TreeNode child in node.Nodes)
                el.AppendChild(SerializeNode(doc, child));
            return el;
        }

        private TreeNode DeserializeNode(XmlNode xnode)
        {
            var node = new TreeNode(xnode.Attributes["Text"].Value)
            {
                Tag = xnode.Attributes["Path"]?.Value
            };
            foreach (XmlNode child in xnode.ChildNodes)
                node.Nodes.Add(DeserializeNode(child));
            return node;
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {

        }
    }
}
