# CaptureLab

Chaîne de capture documentaire en C# : numérisation, OCR, classification, extraction, validation, puis dépôt dans une GED **Alfresco Community** par son API REST et CMIS.

L'application principale est une application **Windows (WinForms, .NET)** de type station de validation : l'opérateur voit le document numérisé, contrôle les champs extraits, les corrige au besoin, puis le dépose dans Alfresco ou l'envoie en exception.

> Toutes les factures et données d'exemple de ce dépôt sont **fictives**.

---

## Fonctionnalités

| Étape | Ce que fait CaptureLab |
|---|---|
| Entrée | Lecture d'un dossier de documents numérisés (TIFF, PNG, JPG ; PDF prévu) |
| OCR | Tesseract (français et anglais), derrière une interface `IOcrEngine` remplaçable |
| Classification | Facture, connaissement, bon de livraison — score par mots-clés et seuil de confiance |
| Extraction | Numéro, date (convertie au format ISO), client, montant total, numéro de conteneur ISO 6346 |
| Validation | Règles métier ; un document non conforme part en file d'exceptions au lieu d'être archivé |
| Dépôt GED | API REST v1 d'Alfresco : authentification par ticket, dépôt multipart avec métadonnées, classement automatique `Type/Année/Mois`, étiquettes, aspect personnalisé, gestion des doublons |
| Recherche | Requêtes AFTS et CMIS-SQL (CMIS 1.1 Browser binding) |
| Robustesse | Reprise automatique sur erreurs transitoires (502, 503, 504, 429), journal par document, rapport CSV |
| Environnements | Configuration séparée test / production ; aucun mot de passe enregistré |

### Architecture

```
 Documents numérisés ──► [IOcrEngine] ──► [KeywordClassifier] ──► [RegexExtractor] ──► [BusinessValidator]
  (tif, png, jpg)          Tesseract        type + confiance        champs              │
                                                                                         ├─ invalide ─► file d'exceptions
                                                                                         └─ valide ───► [AlfrescoExporter]
                                                                                                          │ REST v1 : ticket, multipart,
                                                                                                          │ aspect, étiquettes
                                                                                                          ▼
                                                                         Alfresco : Fichiers partagés/Capture/{Type}/{Année}/{Mois}/
```

---

## Structure de la solution

| Projet | Rôle |
|---|---|
| `CaptureLab.Desktop` | Application Windows (WinForms) : connexion, liste des documents, aperçu, champs extraits, validation |
| `src/CaptureLab.Core` | Modèle, OCR, classification, extraction, validation, orchestration (`CapturePipeline`) |
| `src/CaptureLab.Alfresco` | Client REST v1 + CMIS, reprise sur erreurs (`RetryHandler`), export vers Alfresco |
| `src/CaptureLab.Cli` | Version console (`ping`, `run`, `watch`, `search`, `cmis`), utile pour l'automatisation |
| `tests/CaptureLab.SelfTest` | Banc d'essai avec un faux serveur Alfresco en mémoire (36 vérifications) |
| `postman/` | Collection Postman de l'API Alfresco (16 requêtes) |
| `samples/` | Factures, connaissement et bon de livraison fictifs, cas d'exception |

Les bibliothèques `Core` et `Alfresco` n'utilisent aucun paquet NuGet : uniquement `HttpClient` et `System.Text.Json`.

---

## Prérequis

| Outil | Rôle |
|---|---|
| Windows 10 ou 11 | Application WinForms |
| SDK .NET 8 ou plus récent | Compilation (testé avec .NET 10) |
| Visual Studio 2022 ou plus récent, charge « Développement .NET Desktop » | Développement |
| Tesseract OCR + pack de langue `fra` | OCR des documents numérisés |
| Alfresco Community | GED cible (testé sur 5.2 ; compatible avec les versions récentes en Docker) |
| Postman (facultatif) | Tests manuels de l'API |

---

## Installer Alfresco Community

### Option A — versions récentes, avec Docker (16 Go de RAM recommandés)

```powershell
git clone https://github.com/Alfresco/acs-deployment.git
cd acs-deployment\docker-compose
docker compose -f community-compose.yaml up -d
```

### Option B — poste léger (8 Go de RAM)

L'installateur Windows d'Alfresco Community 5.2 fonctionne avec environ 4 Go de RAM disponibles. Il installe le dépôt, Share, PostgreSQL et Solr. À réserver à un environnement local de test : cette version n'est plus maintenue.

### Après l'installation

1. Changer le mot de passe administrateur.
2. Créer un compte de service dédié à la capture (ex. `capture`), avec les droits *Collaborateur* sur `Fichiers partagés/Capture-TEST` et `Capture`.
3. Vérifier l'API REST dans un navigateur :
   `http://localhost:8080/alfresco/api/-default-/public/alfresco/versions/1/nodes/-root-`

### Modèle de contenu personnalisé (facultatif)

Dans Share → Outils d'administration → Gestionnaire de modèles, créer le modèle `cap` avec l'aspect `cap:documentCapture` :

| Propriété | Type |
|---|---|
| `cap:typeDocument` | d:text |
| `cap:numero` | d:text |
| `cap:dateDocument` | d:date |
| `cap:client` | d:text |
| `cap:montant` | d:double |
| `cap:sha256` | d:text |

Puis renseigner `"CustomAspect": "cap:documentCapture"` dans la configuration.

---

## Utilisation

### Application Windows

1. Ouvrir `CaptureLab.sln` dans Visual Studio et définir `CaptureLab.Desktop` comme projet de démarrage.
2. Lancer (F5), saisir le serveur, l'utilisateur et le mot de passe, puis **Tester la connexion**.
3. Choisir le dossier d'entrée (ex. `C:\CaptureIn`) : les documents s'affichent à gauche.
4. Sélectionner un document, **Traiter**, contrôler les champs, puis **Valider et déposer** ou **Envoyer en exception**.

### Tests sans serveur

```powershell
dotnet run --project tests\CaptureLab.SelfTest
```

### Version console

```powershell
$env:ALFRESCO_USERNAME = "capture"
$env:ALFRESCO_PASSWORD = "<mot de passe>"
dotnet run --project src\CaptureLab.Cli -- run C:\CaptureIn
dotnet run --project src\CaptureLab.Cli -- search "TAG:\"facture\""
```

### Postman

Importer `postman/AlfrescoLab.postman_collection.json`, renseigner `username` et `password` dans **Current value** uniquement, puis exécuter les requêtes dans l'ordre.

---

## Configuration

| Fichier | Rôle |
|---|---|
| `appsettings.json` | Valeurs communes |
| `appsettings.Development.json` | Serveur de test, dossier `Capture-TEST` |
| `appsettings.Production.json` | Serveur de production, aspect personnalisé, seuil de confiance plus strict |

Les identifiants ne sont jamais écrits dans ces fichiers : variables d'environnement `ALFRESCO_USERNAME`, `ALFRESCO_PASSWORD`, `ALFRESCO_BASEURL` (console), ou saisie dans la fenêtre (application Windows).

---

## Dépannage

| Symptôme | Cause probable | Correctif |
|---|---|---|
| 401 Unauthorized | Ticket expiré ou en-tête incorrect | Refaire le ticket ; en-tête `Authorization: Basic base64(ticket)` |
| 403 Forbidden | Mot de passe incorrect ou droits insuffisants | Vérifier le compte et les permissions du dossier |
| 404 sur `relativePath` | Chemin ou alias erroné (`-shared-`, `-my-`, `-root-`) | Tester l'appel dans Postman |
| 409 Conflict | Nom déjà présent dans le dossier | Traité comme doublon par CaptureLab |
| 400 sur `cap:…` | Modèle de contenu non activé | Gestionnaire de modèles → modèle **Actif** |
| Recherche vide juste après un dépôt | Indexation Solr asynchrone | Attendre quelques secondes |
| `Impossible de lancer 'tesseract'` | Tesseract absent du PATH | Indiquer le chemin complet dans `appsettings.json` |
| Accents mal lus | Pack de langue `fra` absent | Réinstaller Tesseract avec le français |

---

## Feuille de route

- [x] Bibliothèques de capture et client Alfresco
- [x] Banc d'essai avec faux serveur
- [x] Fenêtre WinForms : connexion, liste, aperçu, champs extraits
- [ ] Boutons Traiter / Valider et déposer / Envoyer en exception, journal
- [ ] Lecture des PDF (texte natif et PDF numérisés)
- [ ] Extraction des numéros TPS/TVQ, du fournisseur et des modalités de paiement
- [ ] Contrôle des taxes (TPS 5 %, TVQ 9,975 %) et du total
- [ ] Déclaration automatique comme archive (module Records Management)

---

## Licence

Projet personnel d'apprentissage. Données d'exemple fictives.
