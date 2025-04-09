using JerarquiaTaller.Logic;

namespace JerarquiaTaller
{
    partial class FRMTest
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            Arbol = new TreeView();
            Contenedor = new SplitContainer();
            LSTInstance = new ListView();
            CHField = new ColumnHeader();
            CHValue = new ColumnHeader();
            CHComment = new ColumnHeader();
            CHId = new ColumnHeader();
            ((System.ComponentModel.ISupportInitialize)Contenedor).BeginInit();
            Contenedor.Panel1.SuspendLayout();
            Contenedor.Panel2.SuspendLayout();
            Contenedor.SuspendLayout();
            SuspendLayout();
            // 
            // Arbol
            // 
            Arbol.Dock = DockStyle.Fill;
            Arbol.Location = new Point(0, 0);
            Arbol.Name = "Arbol";
            Arbol.Size = new Size(417, 450);
            Arbol.TabIndex = 0;
            Arbol.AfterSelect += Arbol_AfterSelect;
            Arbol.NodeMouseClick += Arbol_NodeMouseClick;
            // 
            // Contenedor
            // 
            Contenedor.Dock = DockStyle.Fill;
            Contenedor.Location = new Point(0, 0);
            Contenedor.Name = "Contenedor";
            // 
            // Contenedor.Panel1
            // 
            Contenedor.Panel1.Controls.Add(Arbol);
            // 
            // Contenedor.Panel2
            // 
            Contenedor.Panel2.Controls.Add(LSTInstance);
            Contenedor.Size = new Size(800, 450);
            Contenedor.SplitterDistance = 417;
            Contenedor.TabIndex = 1;
            // 
            // LSTInstance
            // 
            LSTInstance.Columns.AddRange(new ColumnHeader[] { CHField, CHId, CHValue, CHComment });
            LSTInstance.Dock = DockStyle.Fill;
            LSTInstance.GridLines = true;
            LSTInstance.Location = new Point(0, 0);
            LSTInstance.Name = "LSTInstance";
            LSTInstance.Size = new Size(379, 450);
            LSTInstance.TabIndex = 0;
            LSTInstance.UseCompatibleStateImageBehavior = false;
            LSTInstance.View = View.Details;
            // 
            // CHField
            // 
            CHField.Text = "Campo";
            CHField.Width = 160;
            // 
            // CHValue
            // 
            CHValue.Text = "Valor";
            CHValue.Width = 160;
            // 
            // CHComment
            // 
            CHComment.Text = "Notas";
            CHComment.Width = 160;
            // 
            // CHId
            // 
            CHId.Text = "Id";
            CHId.Width = 80;
            // 
            // FRMTest
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(Contenedor);
            Name = "FRMTest";
            Text = "Verificador de Estructura";
            Contenedor.Panel1.ResumeLayout(false);
            Contenedor.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)Contenedor).EndInit();
            Contenedor.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TreeView Arbol;
        private SplitContainer Contenedor;
        private ListView LSTInstance;
        private ColumnHeader CHField;
        private ColumnHeader CHValue;
        private ColumnHeader CHComment;
        private ColumnHeader CHId;
    }
}
