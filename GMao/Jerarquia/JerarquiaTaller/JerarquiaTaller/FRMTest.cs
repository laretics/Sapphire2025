using JerarquiaTaller.Logic;
using System.Net.Http.Headers;

namespace JerarquiaTaller
{
    public partial class FRMTest : Form
    {
        private Registry? registry = new Registry();
        public FRMTest()
        {
            InitializeComponent();
            HierarchyParser auxParser = new HierarchyParser();
            registry = auxParser.open("structure.xml");
            ImageList listaImagenes = loadImages();
            Arbol.ImageList = listaImagenes;
            if (null != registry && registry.mcolClasses.Count > 0)
            {
                ClassComponent ultima = registry.mcolClasses.Last().Value;
                refreshTree(ultima);
            }

        }
        private ImageList loadImages()
        {
            ImageList salida = new ImageList();
            salida.Images.Add(loadIcon("Circle"));
            salida.Images.Add(loadIcon("Flag"));
            salida.Images.Add(loadIcon("Magnitude"));
            salida.Images.Add(loadIcon("Tree"));
            salida.Images.Add(loadIcon("Wagon"));
            return salida;
        }
        private Icon loadIcon(string iconName)
        {
            string auxPath = System.IO.Path.Combine(AppContext.BaseDirectory, "resources", string.Format("{0}.ico", iconName));
            Icon salida = new Icon(auxPath);
            return salida;
        }

        private void refreshTree(ClassComponent root)
        {
            Arbol.Nodes.Clear();
            Arbol.Nodes.Add(getNode(root, "Estructura"));
        }

        private TreeNode getNode(ClassComponent rhs, string caption)
        {
            TreeNode nodo = new TreeNode();
            nodo.Text = string.Format("{0} ({1})", caption, rhs.Name);
            nodo.Tag = rhs;
            nodo.ImageIndex = 1;
            nodo.SelectedImageIndex = 0;
            if (rhs.Children.Count > 0)
            {
                nodo.ImageIndex = 3;
                foreach (ReferenceInstance child in rhs.Children)
                {
                    nodo.Nodes.Add(getNode(child.Class, child.Name));
                }
            }
            foreach (Magnitude magn in rhs.Magnitudes)
            {
                TreeNode nodoMag = new TreeNode();
                nodoMag.ImageIndex = 2;
                nodoMag.SelectedImageIndex = 2;
                nodoMag.Text = string.Format("{0} ({1}{3}-{2}{3})", magn.name, magn.min, magn.max,magn.units);
                nodo.Nodes.Add(nodoMag);
            }
            if (rhs.Name.ToUpper().StartsWith("UT-") || rhs.Name.ToUpper().StartsWith("COCHE"))
            {
                nodo.ImageIndex = 4;
            }
            return nodo;
        }

        private void showDetails(ClassComponent rhs)
        {
            addField("Class",rhs.StorageId, rhs.Name, rhs.Comment);
            if(null!=rhs.Comment)
            {
                addField("Notes","", rhs.Comment);
            }
            if (rhs.Magnitudes.Count > 0)
            {
                addField("Magnitudes","", string.Format("{0}", rhs.Magnitudes.Count));
                foreach (Magnitude magn in rhs.Magnitudes)
                {
                    if(null==magn.comment)
                        addField("","", magn.name, string.Format("{0}{2} to {1}{2}",magn.min, magn.max,magn.units));
                    else
                        addField("","", magn.name, string.Format("{0}{3} to {1}{3} ({2})", magn.min, magn.max,magn.comment,magn.units));
                }
            }
            if (rhs.Children.Count > 0)
            {
                addField("Components","", string.Format("{0}", rhs.Children.Count));
                foreach(ReferenceInstance auxRef in rhs.Children)
                {
                    addField("",auxRef.Class.StorageId, auxRef.Name, auxRef.comment);
                }
            }           
        }
        private void addField(string fieldName,string id, string fieldValue, string? comments=null)
        {
            ListViewItem nuevo = LSTInstance.Items.Add(fieldName);
            nuevo.SubItems.Add(id);
            nuevo.SubItems.Add(fieldValue);
            if (comments != null) { nuevo.SubItems.Add(comments); }
        }


        private void Arbol_AfterSelect(object sender, TreeViewEventArgs e)
        {

        }

        private void Arbol_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            LSTInstance.Items.Clear();
            if (null != e.Node && null != e.Node.Tag)
            {
                showDetails((ClassComponent)e.Node.Tag);
            }
        }
    }
}
