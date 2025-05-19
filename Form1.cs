using System;
using System.IO;
using System.Windows.Forms;

namespace FileTreeEditor
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            IsMdiContainer = true;
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var child = new TreeEditorForm();
            child.MdiParent = this;
            child.Text = "Новый документ";
            child.Show();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "Tree файлы|*.treexml" })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var child = new TreeEditorForm();
                    child.MdiParent = this;
                    child.LoadTreeFromFile(dlg.FileName);
                    child.Text = Path.GetFileName(dlg.FileName);
                    child.Show();
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (ActiveMdiChild is TreeEditorForm child)
                child.SaveTreeToFile();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}