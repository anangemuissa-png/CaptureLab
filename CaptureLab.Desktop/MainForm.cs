using CaptureLab.Alfresco;

namespace CaptureLab.Desktop;

public partial class MainForm : Form
{
    // Zone "Validation" créée par le code (sous la grille des champs)
    private Label lblValidation = null!;
    private ListBox lstValidation = null!;

    // Client Alfresco partagé par toute la fenêtre (créé au moment de la connexion)
    private AlfrescoClient? _client;

    public MainForm()
    {
        InitializeComponent();
        CreerZoneValidation();
        cmbEnvironnement.SelectedIndex = 0;   // "Development" par défaut
        AfficherEtat(false, "Non connecté");
        // 1. Dans le constructeur, après AfficherEtat(false, "Non connecté");
        ChargerDocuments();
    }

    private async void btnTester_Click(object sender, EventArgs e)
    {
        btnTester.Enabled = false;
        lblEtat.Text = "● Connexion…";
        lblEtat.ForeColor = Color.DimGray;

        try
        {
            _client?.Dispose();

            var options = new AlfrescoOptions
            {
                BaseUrl = txtServeur.Text.Trim(),
                Username = txtUtilisateur.Text.Trim(),
                Password = txtMotDePasse.Text,          // jamais enregistré sur disque
                RootFolder = cmbEnvironnement.Text == "Production" ? "Capture" : "Capture-TEST",
                TimeoutSeconds = 15,
                MaxRetries = 1
            };

            _client = new AlfrescoClient(options);
            await _client.LoginAsync();                          // POST /tickets
            var racine = await _client.GetNodeAsync("-root-");   // GET /nodes/-root-

            AfficherEtat(true, $"Connecté ({racine.Name})");
        }
        catch (Exception ex)
        {
            AfficherEtat(false, "Non connecté");
            MessageBox.Show(this, ex.Message, "Connexion Alfresco",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            btnTester.Enabled = true;
        }
    }

    private void AfficherEtat(bool connecte, string texte)
    {
        lblEtat.Text = "● " + texte;
        lblEtat.ForeColor = connecte ? Color.ForestGreen : Color.Firebrick;
    }
    private void CreerZoneValidation()
    {
        // Le cadre qui contient la grille des champs (grpChamps)
        // Le cadre qui contient la grille des champs.
        // Si la grille est posée directement dans une cellule de tlpCentre,
        // on crée le cadre "Champs extraits" dans cette cellule et on y déplace la grille.
        Control conteneur = tlpChamps.Parent!;
        if (conteneur is TableLayoutPanel grille)
        {
            var cellule = grille.GetPositionFromControl(tlpChamps);
            var cadre = new GroupBox { Text = "Champs extraits", Dock = DockStyle.Fill };

            grille.Controls.Remove(tlpChamps);
            grille.Controls.Add(cadre, cellule.Column, cellule.Row);
            cadre.Controls.Add(tlpChamps);

            conteneur = cadre;
        }

        // La grille prend toute la largeur, en haut du cadre
        tlpChamps.Dock = DockStyle.Top;

        // Les zones de texte et la barre de confiance s'étirent sur toute la colonne
        foreach (Control c in tlpChamps.Controls)
            if (c is TextBox or System.Windows.Forms.ProgressBar)
                c.Dock = DockStyle.Fill;

        lblValidation = new Label
        {
            Text = "Validation",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 30,
            ForeColor = Color.Navy,
            Padding = new Padding(2, 10, 0, 0)
        };

        lstValidation = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false
        };

        conteneur.Controls.Add(lstValidation);
        conteneur.Controls.Add(lblValidation);

        // Ordre d'empilement : grille en haut, puis le titre, puis la liste qui remplit le reste
        tlpChamps.SendToBack();
        lstValidation.BringToFront();
    }
    private void cmbEnvironnement_SelectedIndexChanged(object sender, EventArgs e)
    {

    }

    private void panel1_Paint(object sender, PaintEventArgs e)
    {

    }

    private void button1_Click(object sender, EventArgs e)
    {
        using var dialogue = new FolderBrowserDialog
        {
            Description = "Choisir le dossier des documents numérisés",
            UseDescriptionForTitle = true,
            InitialDirectory = txtDossier.Text,   // ouvre la fenêtre DANS ce dossier
            SelectedPath = txtDossier.Text
        };

        if (dialogue.ShowDialog(this) == DialogResult.OK)
            txtDossier.Text = dialogue.SelectedPath;
    }

    private void btnParcourir_Click(object sender, EventArgs e)
    {
        using var dialogue = new FolderBrowserDialog
        {
            Description = "Choisir le dossier des documents numérisés",
            UseDescriptionForTitle = true,
            InitialDirectory = txtDossier.Text,   // ouvre la fenêtre DANS ce dossier
            SelectedPath = txtDossier.Text
        };

        if (dialogue.ShowDialog(this) == DialogResult.OK)
            txtDossier.Text = dialogue.SelectedPath;
        ChargerDocuments();
    }

    private void tlpCentre_Paint(object sender, PaintEventArgs e)
    {

    }
    // Formats lus pour l'instant (le PDF viendra dans une étape dédiée)
    private static readonly string[] ExtensionsImages = [".tif", ".tiff", ".png", ".jpg", ".jpeg"];

    private void ChargerDocuments()
    {
        lvDocuments.Items.Clear();
        picApercu.Image?.Dispose();
        picApercu.Image = null;

        if (!Directory.Exists(txtDossier.Text))
            return;

        var fichiers = Directory.EnumerateFiles(txtDossier.Text)
            .Where(f => ExtensionsImages.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f);

        foreach (var chemin in fichiers)
        {
            var ligne = new ListViewItem(Path.GetFileName(chemin)) { Tag = chemin };
            ligne.SubItems.Add("En attente");
            lvDocuments.Items.Add(ligne);
        }
    }

    private void lvDocuments_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lvDocuments.SelectedItems.Count == 0)
            return;

        var chemin = (string)lvDocuments.SelectedItems[0].Tag!;

        picApercu.Image?.Dispose();
        // On copie l'image en mémoire pour ne pas verrouiller le fichier :
        // plus tard, CaptureLab devra pouvoir le déplacer vers _traites ou _exceptions.
        using var flux = File.OpenRead(chemin);
        using var image = Image.FromStream(flux);
        picApercu.Image = new Bitmap(image);
    }

    private void label3_Click(object sender, EventArgs e)
    {

    }

    private void label7_Click(object sender, EventArgs e)
    {

    }

    private void label9_Click(object sender, EventArgs e)
    {

    }
}
