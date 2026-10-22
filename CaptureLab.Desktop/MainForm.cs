using CaptureLab.Alfresco;
using CaptureLab.Core;


namespace CaptureLab.Desktop;

public partial class MainForm : Form
{
    // Client Alfresco partagé par toute la fenêtre (créé au moment de la connexion)
    private AlfrescoClient? _client;

    // Zone "Validation" créée par le code (sous la grille des champs)
    private Label lblValidation = null!;
    private ListBox lstValidation = null!;

    // Formats lus pour l'instant (le PDF viendra dans une étape dédiée)
    private static readonly string[] ExtensionsImages = [".tif", ".tiff", ".png", ".jpg", ".jpeg"];
    // Chemin de Tesseract installé à l'étape 10A
    private const string CheminTesseract = @"C:\Program Files\Tesseract-OCR\tesseract.exe";

    // Chaîne de capture SANS dépôt : le dépôt se fera au clic sur "Valider et déposer"
    private readonly CapturePipeline _pipeline = new(
        new CompositeOcrEngine(new TesseractCliOcrEngine(CheminTesseract, "fra+eng")),
        new KeywordClassifier(),
        new RegexExtractor(),
        new BusinessValidator(),
        exporter: null);

    // Dernier document traité (servira au dépôt à l'étape 11)
    private CaptureDocument? _documentCourant;

    public MainForm()
    {
        InitializeComponent();          // crée les contrôles dessinés dans le concepteur
        CreerZoneValidation();          // étape 8B : titre "Validation" + liste
        OrganiserDisposition();         // étape 9 : boutons, journal, barre d'état au bon endroit

        cmbEnvironnement.SelectedIndex = 0;   // "Development" par défaut
        AfficherEtat(false, "Non connecté");
        ChargerDocuments();                   // lit C:\CaptureIn

        tsslEnvironnement.Text = "Environnement : " + cmbEnvironnement.Text;
        MettreAJourBoutons();
    }

    #region Connexion Alfresco (étape 6)

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
            tsslEnvironnement.Text = "Environnement : " + cmbEnvironnement.Text;
            Journaliser($"Connexion réussie ({racine.Name}) – {txtServeur.Text}");
        }
        catch (Exception ex)
        {
            AfficherEtat(false, "Non connecté");
            Journaliser("Échec de connexion : " + ex.Message);
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

    #endregion

    #region Dossier d'entrée, liste et aperçu (étapes 7 et 8A)

    // Bouton "Parcourir…"
    private void btnParcourir_Click(object sender, EventArgs e)
    {
        using var dialogue = new FolderBrowserDialog
        {
            Description = "Choisir le dossier des documents numérisés",
            UseDescriptionForTitle = true,
            InitialDirectory = txtDossier.Text,
            SelectedPath = txtDossier.Text
        };

        if (dialogue.ShowDialog(this) == DialogResult.OK)
        {
            txtDossier.Text = dialogue.SelectedPath;
            ChargerDocuments();
        }
    }

    private void ChargerDocuments()
    {
        lvDocuments.Items.Clear();
        picApercu.Image?.Dispose();
        picApercu.Image = null;

        if (Directory.Exists(txtDossier.Text))
        {
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

        tsslDocuments.Text = $"{lvDocuments.Items.Count} document(s)";
        MettreAJourBoutons();
    }

    private void lvDocuments_SelectedIndexChanged(object sender, EventArgs e)
    {
        ViderChamps();
        MettreAJourBoutons();


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

    #endregion

    #region Disposition de la fenêtre (étapes 8B et 9)

    private void CreerZoneValidation()
    {
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

    private void OrganiserDisposition()
    {
        // 1. Ces trois zones doivent être posées directement sur la fenêtre
        foreach (Control zone in new Control[] { ssEtat, grpJournal, flpActions })
        {
            if (zone.Parent != this)
            {
                zone.Parent?.Controls.Remove(zone);
                Controls.Add(zone);
            }
        }

        flpActions.Dock = DockStyle.Bottom;
        flpActions.Height = 44;
        grpJournal.Dock = DockStyle.Bottom;
        grpJournal.Height = 150;

        // 2. Ordre d'empilement, du fond vers l'avant :
        //    en haut : connexion, puis dossier d'entrée
        //    en bas  : barre d'état, puis journal, puis boutons
        //    au centre : la grille, qui remplit l'espace restant
        var panneauDossier = txtDossier.Parent!;

        grpConnexion.BringToFront();
        panneauDossier.BringToFront();
        ssEtat.BringToFront();
        grpJournal.BringToFront();
        flpActions.BringToFront();
        tlpCentre.BringToFront();
    }

    #endregion

    #region Journal et boutons d'action (étape 9)

    private void Journaliser(string message)
    {
        txtJournal.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
    }

    private void MettreAJourBoutons()
    {
        bool documentChoisi = lvDocuments.SelectedItems.Count > 0;
        btnTraiter.Enabled = documentChoisi;
        btnException.Enabled = documentChoisi;

        // "Valider et déposer" seulement si le document sélectionné vient d'être traité ET est valide
        btnValider.Enabled = documentChoisi
            && _documentCourant?.Status == CaptureStatus.Valide
            && _documentCourant.SourcePath == (string)lvDocuments.SelectedItems[0].Tag!;
    }

    private async void btnTraiter_Click(object sender, EventArgs e)
    {
        if (lvDocuments.SelectedItems.Count == 0)
            return;

        var ligne = lvDocuments.SelectedItems[0];
        var chemin = (string)ligne.Tag!;

        Cursor = Cursors.WaitCursor;
        btnTraiter.Enabled = false;
        ViderChamps();

        try
        {
            var doc = await _pipeline.ProcessAsync(chemin);   // OCR → classification → extraction → validation
            _documentCourant = doc;

            // Copie la trace du traitement dans le journal (sans l'heure, déjà ajoutée par Journaliser)
            foreach (var trace in doc.Log)
                Journaliser($"{doc.FileName} : {trace[9..]}");

            AfficherResultat(doc);

            ligne.SubItems[1].Text = doc.Status switch
            {
                CaptureStatus.Valide => "Valide",
                CaptureStatus.EnException => "Exception",
                CaptureStatus.Erreur => "Erreur",
                _ => doc.Status.ToString()
            };
        }
        finally
        {
            Cursor = Cursors.Default;
            MettreAJourBoutons();
        }
    }
    private void AfficherResultat(CaptureDocument doc)
    {
        lblConfiance.Text = doc.Classification?.Type.ToString() ?? "";
        txtNumero.Text = doc["numero"] ?? "";
        txtDate.Text = doc["date"] ?? "";
        txtClient.Text = doc["client"] ?? "";
        txtMontant.Text = doc["montant"] ?? "";
        barConfiance.Value = (int)Math.Round((doc.Classification?.Confidence ?? 0) * 100);

        // Les champs signalés par la validation passent en rouge pâle
        var erreurs = doc.Validation?.Errors ?? [];
        foreach (var (zone, champ) in new[] { (txtNumero, "numero"), (txtDate, "date"), (txtMontant, "montant") })
            zone.BackColor = erreurs.Any(err => err.Contains(champ)) ? Color.MistyRose : SystemColors.Window;

        lstValidation.Items.Clear();
        if (doc.Status == CaptureStatus.Erreur)
            lstValidation.Items.Add("✖ Erreur de traitement : voir le journal");
        else if (doc.Validation is { IsValid: true })
            lstValidation.Items.Add("✔ Toutes les règles sont respectées");
        else
            foreach (var err in erreurs)
                lstValidation.Items.Add("✖ " + err);
    }

    private void ViderChamps()
    {
        foreach (Control zone in new Control[] { lblConfiance, txtNumero, txtDate, txtClient, txtMontant })
        {
            zone.Text = "";
            zone.BackColor = zone is TextBoxBase { ReadOnly: true }
                ? SystemColors.Control
                : SystemColors.Window;
        }
        barConfiance.Value = 0;
        lstValidation.Items.Clear();
    }
    private void btnValider_Click(object sender, EventArgs e)
    {
        Journaliser("Valider et déposer : sera branché à l'étape 11.");
    }

    private void btnException_Click(object sender, EventArgs e)
    {
        Journaliser("Envoyer en exception : sera branché à l'étape 11.");
    }

    private void btnShare_Click(object sender, EventArgs e)
    {
        var adresse = txtServeur.Text.TrimEnd('/') + "/share";
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(adresse) { UseShellExecute = true });
    }

    #endregion
}