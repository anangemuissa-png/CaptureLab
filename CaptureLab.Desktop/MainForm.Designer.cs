namespace CaptureLab.Desktop
{
    partial class MainForm
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
            components = new System.ComponentModel.Container();
            grpConnexion = new GroupBox();
            flpConnexion = new FlowLayoutPanel();
            lblServeur = new Label();
            txtServeur = new TextBox();
            lblUtilisateur = new Label();
            txtUtilisateur = new TextBox();
            lblMotDePasse = new Label();
            txtMotDePasse = new TextBox();
            lblEnvironnement = new Label();
            cmbEnvironnement = new ComboBox();
            btnTester = new Button();
            lblEtat = new Label();
            bindingSource1 = new BindingSource(components);
            pnlDossier = new Panel();
            lblFormats = new Label();
            btnParcourir = new Button();
            txtDossier = new TextBox();
            lblDossier = new Label();
            tlpCentre = new TableLayoutPanel();
            lvDocuments = new ListView();
            columnHeader1 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            picApercu = new PictureBox();
            tlpChamps = new TableLayoutPanel();
            txtMontant = new TextBox();
            txtClient = new TextBox();
            label9 = new Label();
            label7 = new Label();
            label5 = new Label();
            label3 = new Label();
            label1 = new Label();
            txtType = new Label();
            txtNumero = new TextBox();
            texttype = new TextBox();
            txtDate = new TextBox();
            ProgressBar = new ProgressBar();
            grpConnexion.SuspendLayout();
            flpConnexion.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)bindingSource1).BeginInit();
            pnlDossier.SuspendLayout();
            tlpCentre.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picApercu).BeginInit();
            tlpChamps.SuspendLayout();
            SuspendLayout();
            // 
            // grpConnexion
            // 
            grpConnexion.Controls.Add(flpConnexion);
            grpConnexion.Dock = DockStyle.Top;
            grpConnexion.Location = new Point(0, 0);
            grpConnexion.Name = "grpConnexion";
            grpConnexion.Size = new Size(1182, 125);
            grpConnexion.TabIndex = 0;
            grpConnexion.TabStop = false;
            grpConnexion.Text = "Connexion Alfresco";
            // 
            // flpConnexion
            // 
            flpConnexion.Controls.Add(lblServeur);
            flpConnexion.Controls.Add(txtServeur);
            flpConnexion.Controls.Add(lblUtilisateur);
            flpConnexion.Controls.Add(txtUtilisateur);
            flpConnexion.Controls.Add(lblMotDePasse);
            flpConnexion.Controls.Add(txtMotDePasse);
            flpConnexion.Controls.Add(lblEnvironnement);
            flpConnexion.Controls.Add(cmbEnvironnement);
            flpConnexion.Controls.Add(btnTester);
            flpConnexion.Controls.Add(lblEtat);
            flpConnexion.Dock = DockStyle.Fill;
            flpConnexion.Location = new Point(3, 23);
            flpConnexion.Name = "flpConnexion";
            flpConnexion.Size = new Size(1176, 99);
            flpConnexion.TabIndex = 0;
            flpConnexion.WrapContents = false;
            // 
            // lblServeur
            // 
            lblServeur.AutoSize = true;
            lblServeur.Location = new Point(3, 8);
            lblServeur.Margin = new Padding(3, 8, 3, 3);
            lblServeur.Name = "lblServeur";
            lblServeur.Size = new Size(65, 20);
            lblServeur.TabIndex = 0;
            lblServeur.Text = "Serveur :";
            // 
            // txtServeur
            // 
            txtServeur.Location = new Point(74, 3);
            txtServeur.Name = "txtServeur";
            txtServeur.Size = new Size(152, 27);
            txtServeur.TabIndex = 1;
            txtServeur.Text = "http://localhost:8080";
            // 
            // lblUtilisateur
            // 
            lblUtilisateur.AutoSize = true;
            lblUtilisateur.Location = new Point(232, 8);
            lblUtilisateur.Margin = new Padding(3, 8, 3, 3);
            lblUtilisateur.Name = "lblUtilisateur";
            lblUtilisateur.Size = new Size(83, 20);
            lblUtilisateur.TabIndex = 1;
            lblUtilisateur.Text = "Utilisateur :";
            // 
            // txtUtilisateur
            // 
            txtUtilisateur.Location = new Point(321, 3);
            txtUtilisateur.MaximumSize = new Size(110, 0);
            txtUtilisateur.Name = "txtUtilisateur";
            txtUtilisateur.Size = new Size(82, 27);
            txtUtilisateur.TabIndex = 2;
            txtUtilisateur.Text = "admin";
            // 
            // lblMotDePasse
            // 
            lblMotDePasse.AutoSize = true;
            lblMotDePasse.Location = new Point(409, 8);
            lblMotDePasse.Margin = new Padding(3, 8, 3, 3);
            lblMotDePasse.Name = "lblMotDePasse";
            lblMotDePasse.Size = new Size(105, 20);
            lblMotDePasse.TabIndex = 3;
            lblMotDePasse.Text = "Mot de passe :";
            // 
            // txtMotDePasse
            // 
            txtMotDePasse.Location = new Point(520, 3);
            txtMotDePasse.MaximumSize = new Size(110, 0);
            txtMotDePasse.Name = "txtMotDePasse";
            txtMotDePasse.Size = new Size(87, 27);
            txtMotDePasse.TabIndex = 4;
            txtMotDePasse.UseSystemPasswordChar = true;
            // 
            // lblEnvironnement
            // 
            lblEnvironnement.AutoSize = true;
            lblEnvironnement.Location = new Point(613, 8);
            lblEnvironnement.Margin = new Padding(3, 8, 3, 3);
            lblEnvironnement.Name = "lblEnvironnement";
            lblEnvironnement.Size = new Size(115, 20);
            lblEnvironnement.TabIndex = 5;
            lblEnvironnement.Text = "Environnement :";
            // 
            // cmbEnvironnement
            // 
            cmbEnvironnement.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbEnvironnement.FormattingEnabled = true;
            cmbEnvironnement.Items.AddRange(new object[] { "Development", "Production" });
            cmbEnvironnement.Location = new Point(734, 3);
            cmbEnvironnement.Name = "cmbEnvironnement";
            cmbEnvironnement.Size = new Size(137, 28);
            cmbEnvironnement.TabIndex = 6;
            cmbEnvironnement.SelectedIndexChanged += cmbEnvironnement_SelectedIndexChanged;
            // 
            // btnTester
            // 
            btnTester.AutoSize = true;
            btnTester.Location = new Point(877, 3);
            btnTester.Name = "btnTester";
            btnTester.Size = new Size(146, 30);
            btnTester.TabIndex = 7;
            btnTester.Text = "Tester la connexion";
            btnTester.UseVisualStyleBackColor = true;
            btnTester.Click += btnTester_Click;
            // 
            // lblEtat
            // 
            lblEtat.AutoSize = true;
            lblEtat.Location = new Point(1029, 8);
            lblEtat.Margin = new Padding(3, 8, 3, 3);
            lblEtat.Name = "lblEtat";
            lblEtat.Size = new Size(114, 20);
            lblEtat.TabIndex = 8;
            lblEtat.Text = "● Non connecté";
            // 
            // pnlDossier
            // 
            pnlDossier.Controls.Add(lblFormats);
            pnlDossier.Controls.Add(btnParcourir);
            pnlDossier.Controls.Add(txtDossier);
            pnlDossier.Controls.Add(lblDossier);
            pnlDossier.Dock = DockStyle.Top;
            pnlDossier.Location = new Point(0, 125);
            pnlDossier.Name = "pnlDossier";
            pnlDossier.Size = new Size(1182, 40);
            pnlDossier.TabIndex = 1;
            pnlDossier.Paint += panel1_Paint;
            // 
            // lblFormats
            // 
            lblFormats.AutoSize = true;
            lblFormats.Location = new Point(504, 8);
            lblFormats.Margin = new Padding(3, 8, 3, 3);
            lblFormats.Name = "lblFormats";
            lblFormats.Size = new Size(260, 20);
            lblFormats.TabIndex = 9;
            lblFormats.Text = "Formats acceptés : TIFF, PNG, JPG, PDF";
            // 
            // btnParcourir
            // 
            btnParcourir.AutoSize = true;
            btnParcourir.Location = new Point(352, 3);
            btnParcourir.Name = "btnParcourir";
            btnParcourir.Size = new Size(146, 30);
            btnParcourir.TabIndex = 8;
            btnParcourir.Text = "Parcourir…";
            btnParcourir.UseVisualStyleBackColor = true;
            btnParcourir.Click += btnParcourir_Click;
            // 
            // txtDossier
            // 
            txtDossier.Location = new Point(130, 5);
            txtDossier.Name = "txtDossier";
            txtDossier.Size = new Size(216, 27);
            txtDossier.TabIndex = 3;
            txtDossier.Text = "C:\\CaptureIn";
            // 
            // lblDossier
            // 
            lblDossier.AutoSize = true;
            lblDossier.Location = new Point(3, 8);
            lblDossier.Margin = new Padding(3, 8, 3, 3);
            lblDossier.Name = "lblDossier";
            lblDossier.Size = new Size(123, 20);
            lblDossier.TabIndex = 2;
            lblDossier.Text = "Dossier d'entrée :";
            // 
            // tlpCentre
            // 
            tlpCentre.CellBorderStyle = TableLayoutPanelCellBorderStyle.Inset;
            tlpCentre.ColumnCount = 3;
            tlpCentre.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
            tlpCentre.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpCentre.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340F));
            tlpCentre.Controls.Add(lvDocuments, 0, 0);
            tlpCentre.Controls.Add(picApercu, 1, 0);
            tlpCentre.Controls.Add(tlpChamps, 2, 0);
            tlpCentre.Dock = DockStyle.Fill;
            tlpCentre.Location = new Point(0, 165);
            tlpCentre.Name = "tlpCentre";
            tlpCentre.Padding = new Padding(6);
            tlpCentre.RowCount = 1;
            tlpCentre.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tlpCentre.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            tlpCentre.Size = new Size(1182, 648);
            tlpCentre.TabIndex = 2;
            tlpCentre.Paint += tlpCentre_Paint;
            // 
            // lvDocuments
            // 
            lvDocuments.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2 });
            lvDocuments.Dock = DockStyle.Fill;
            lvDocuments.FullRowSelect = true;
            lvDocuments.Location = new Point(11, 11);
            lvDocuments.MultiSelect = false;
            lvDocuments.Name = "lvDocuments";
            lvDocuments.Size = new Size(294, 626);
            lvDocuments.TabIndex = 0;
            lvDocuments.UseCompatibleStateImageBehavior = false;
            lvDocuments.View = View.Details;
            lvDocuments.SelectedIndexChanged += lvDocuments_SelectedIndexChanged;
            // 
            // columnHeader1
            // 
            columnHeader1.Text = "Fichier";
            columnHeader1.Width = 190;
            // 
            // columnHeader2
            // 
            columnHeader2.Text = "Statut";
            // 
            // picApercu
            // 
            picApercu.BackColor = SystemColors.GrayText;
            picApercu.BorderStyle = BorderStyle.FixedSingle;
            picApercu.Dock = DockStyle.Fill;
            picApercu.Location = new Point(313, 11);
            picApercu.Name = "picApercu";
            picApercu.Size = new Size(516, 626);
            picApercu.SizeMode = PictureBoxSizeMode.Zoom;
            picApercu.TabIndex = 1;
            picApercu.TabStop = false;
            // 
            // tlpChamps
            // 
            tlpChamps.AutoSize = true;
            tlpChamps.ColumnCount = 2;
            tlpChamps.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            tlpChamps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpChamps.Controls.Add(txtMontant, 1, 4);
            tlpChamps.Controls.Add(txtClient, 1, 3);
            tlpChamps.Controls.Add(label9, 0, 4);
            tlpChamps.Controls.Add(label7, 0, 3);
            tlpChamps.Controls.Add(label5, 0, 2);
            tlpChamps.Controls.Add(label3, 0, 1);
            tlpChamps.Controls.Add(label1, 0, 0);
            tlpChamps.Controls.Add(txtType, 0, 5);
            tlpChamps.Controls.Add(txtNumero, 1, 1);
            tlpChamps.Controls.Add(texttype, 1, 0);
            tlpChamps.Controls.Add(txtDate, 1, 2);
            tlpChamps.Controls.Add(ProgressBar, 1, 5);
            tlpChamps.Location = new Point(837, 11);
            tlpChamps.Name = "tlpChamps";
            tlpChamps.RowCount = 6;
            tlpChamps.RowStyles.Add(new RowStyle());
            tlpChamps.RowStyles.Add(new RowStyle());
            tlpChamps.RowStyles.Add(new RowStyle());
            tlpChamps.RowStyles.Add(new RowStyle());
            tlpChamps.RowStyles.Add(new RowStyle());
            tlpChamps.RowStyles.Add(new RowStyle());
            tlpChamps.Size = new Size(221, 196);
            tlpChamps.TabIndex = 2;
            // 
            // txtMontant
            // 
            txtMontant.Location = new Point(93, 135);
            txtMontant.Name = "txtMontant";
            txtMontant.Size = new Size(125, 27);
            txtMontant.TabIndex = 23;
            // 
            // txtClient
            // 
            txtClient.Location = new Point(93, 102);
            txtClient.Name = "txtClient";
            txtClient.Size = new Size(125, 27);
            txtClient.TabIndex = 22;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(3, 140);
            label9.Margin = new Padding(3, 8, 3, 3);
            label9.Name = "label9";
            label9.Size = new Size(72, 20);
            label9.TabIndex = 18;
            label9.Text = "Montant :";
            label9.Click += label9_Click;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(3, 107);
            label7.Margin = new Padding(3, 8, 3, 3);
            label7.Name = "label7";
            label7.Size = new Size(54, 20);
            label7.TabIndex = 16;
            label7.Text = "Client :";
            label7.Click += label7_Click;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(3, 74);
            label5.Margin = new Padding(3, 8, 3, 3);
            label5.Name = "label5";
            label5.Size = new Size(48, 20);
            label5.TabIndex = 14;
            label5.Text = "Date :";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(3, 41);
            label3.Margin = new Padding(3, 8, 3, 3);
            label3.Name = "label3";
            label3.Size = new Size(70, 20);
            label3.TabIndex = 12;
            label3.Text = "Numéro :";
            label3.Click += label3_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(3, 8);
            label1.Margin = new Padding(3, 8, 3, 3);
            label1.Name = "label1";
            label1.Size = new Size(47, 20);
            label1.TabIndex = 10;
            label1.Text = "Type :";
            // 
            // txtType
            // 
            txtType.AutoSize = true;
            txtType.Location = new Point(3, 173);
            txtType.Margin = new Padding(3, 8, 3, 3);
            txtType.Name = "txtType";
            txtType.Size = new Size(82, 20);
            txtType.TabIndex = 9;
            txtType.Text = "Confiance :";
            // 
            // txtNumero
            // 
            txtNumero.Location = new Point(93, 36);
            txtNumero.Name = "txtNumero";
            txtNumero.Size = new Size(125, 27);
            txtNumero.TabIndex = 19;
            // 
            // texttype
            // 
            texttype.Location = new Point(93, 3);
            texttype.Name = "texttype";
            texttype.ReadOnly = true;
            texttype.Size = new Size(125, 27);
            texttype.TabIndex = 20;
            // 
            // txtDate
            // 
            txtDate.Location = new Point(93, 69);
            txtDate.Name = "txtDate";
            txtDate.Size = new Size(125, 27);
            txtDate.TabIndex = 21;
            // 
            // ProgressBar
            // 
            ProgressBar.Location = new Point(93, 168);
            ProgressBar.Name = "ProgressBar";
            ProgressBar.Size = new Size(125, 18);
            ProgressBar.TabIndex = 24;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1182, 813);
            Controls.Add(tlpCentre);
            Controls.Add(pnlDossier);
            Controls.Add(grpConnexion);
            MinimumSize = new Size(1000, 700);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "CaptureLab – Capture documentaire vers Alfresco";
            grpConnexion.ResumeLayout(false);
            flpConnexion.ResumeLayout(false);
            flpConnexion.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)bindingSource1).EndInit();
            pnlDossier.ResumeLayout(false);
            pnlDossier.PerformLayout();
            tlpCentre.ResumeLayout(false);
            tlpCentre.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picApercu).EndInit();
            tlpChamps.ResumeLayout(false);
            tlpChamps.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private GroupBox grpConnexion;
        private FlowLayoutPanel flpConnexion;
        private Label lblServeur;
        private TextBox txtServeur;
        private Label lblUtilisateur;
        private TextBox txtUtilisateur;
        private Label lblMotDePasse;
        private TextBox txtMotDePasse;
        private Label lblEnvironnement;
        private ComboBox cmbEnvironnement;
        private Button btnTester;
        private Label lblEtat;
        private BindingSource bindingSource1;
        private Panel pnlDossier;
        private Label lblDossier;
        private TextBox txtDossier;
        private Label lblFormats;
        private Button btnParcourir;
        private TableLayoutPanel tlpCentre;
        private ListView lvDocuments;
        private ColumnHeader columnHeader1;
        private ColumnHeader columnHeader2;
        private PictureBox picApercu;
        private TableLayoutPanel tlpChamps;
        private Label label7;
        private Label label5;
        private Label label3;
        private Label label1;
        private Label txtType;
        private Label label9;
        private TextBox txtMontant;
        private TextBox txtClient;
        private TextBox txtNumero;
        private TextBox texttype;
        private TextBox txtDate;
        private ProgressBar ProgressBar;
    }
}
