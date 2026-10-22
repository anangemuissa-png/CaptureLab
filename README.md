# Labo Alfresco + C# — préparation au test technique

Refaire en **C# seul** la chaîne de capture de 2023 (projet Millodeploie) : numérisation → OCR → classification → extraction → validation → dépôt dans **Alfresco Community** par **API REST** et requêtes **CMIS**, avec séparation **test / production**.

> Rien dans ce dépôt ne contient de mot de passe ni d'adresse de serveur réelle. Les identifiants passent par des variables d'environnement.

---

## 1. Ce qui change par rapport à 2023

| Étape | 2023 (Millodeploie) | 2026 (ce labo) | Ce que ça montre au test |
|---|---|---|---|
| Numérisation / entrée | Poste WinDev + scanner | Dossier surveillé (`watch`) ou lot (`run`) | Notion de *hot folder* / lot de capture |
| OCR | WinDev | Tesseract derrière l'interface `IOcrEngine` | Moteur OCR remplaçable (Tesseract, Azure, Tungsten…) |
| Classification | Manuelle / règles | `KeywordClassifier` : score par classe + seuil | Confiance et seuil, comme un outil IDP |
| Extraction | Indexation Informix / HFSQL | `RegexExtractor` (numéro, date, client, montant, conteneur ISO 6346) | Localisateurs de champs, normalisation |
| Validation | Contrôle opérateur | `BusinessValidator` → statut `EnException` | File de validation humaine |
| Dépôt GED | Bibliothèque C# → Alfresco (REST + CMIS) | `AlfrescoClient` .NET 8 sans dépendance | Ticket, multipart, aspects, versions, recherche |
| Environnements | Serveurs test et prod séparés | `appsettings.{Development,Production}.json` + variables | Déploiement sans secret dans le code |
| Tests | Postman | Postman + banc d'essai `SelfTest` (36 vérifications) | Tests reproductibles, faux serveur |

### Architecture

```
 Fichiers numérisés ──► [IOcrEngine] ──► [KeywordClassifier] ──► [RegexExtractor] ──► [BusinessValidator]
  (png, tif, jpg, txt)     Tesseract        type + confiance        champs              │
                                                                                         ├─ invalide ─► _exceptions/  (validation humaine)
                                                                                         └─ valide ───► [AlfrescoExporter]
                                                                                                          │ REST v1 : ticket, multipart,
                                                                                                          │ aspect cap:documentCapture, tags
                                                                                                          ▼
                                                                    Alfresco : Dossiers partagés/Capture/{Type}/{Année}/{Mois}/
```

### Structure de la solution

| Projet | Rôle |
|---|---|
| `src/CaptureLab.Core` | Modèle, OCR, classification, extraction, validation, orchestration (`CapturePipeline`) |
| `src/CaptureLab.Alfresco` | Client REST v1 + CMIS Browser binding, reprise sur erreurs (`RetryHandler`), export |
| `src/CaptureLab.Cli` | Console `capturelab` : `ping`, `run`, `watch`, `search`, `cmis` |
| `tests/CaptureLab.SelfTest` | Banc d'essai avec faux serveur Alfresco en mémoire |
| `postman/` | Collection Postman (16 requêtes) |
| `samples/` | Facture, connaissement, bon de livraison, cas d'exception, scans PNG |

---

## 2. Prérequis (Windows)

| Outil | Pourquoi | Vérification |
|---|---|---|
| Docker Desktop (moteur WSL 2) | Faire tourner Alfresco | `docker version` |
| **RAM : au moins 16 Go alloués à Docker** | La documentation officielle demande environ 13 Go libres | Docker Desktop → Settings → Resources |
| Git | Récupérer le déploiement officiel | `git --version` |
| SDK .NET 8 | Compiler CaptureLab | `dotnet --version` |
| Tesseract OCR + langue française | OCR des scans | `tesseract --list-langs` doit afficher `fra` |
| Postman | Tester l'API à la main | — |

> ⚠ **À vérifier sur ton poste** : si ton PC a 16 Go de RAM au total, Alfresco va être serré (conteneurs qui s'arrêtent sans message). Ferme les autres applications, ou loue une VM temporaire (Azure/AWS, 16 Go) le temps des révisions.

---

## 3. Installer Alfresco Community avec Docker

On utilise le **déploiement officiel** d'Alfresco plutôt qu'un fichier maison : c'est ce qu'un intégrateur ferait chez un client, et les versions des images restent cohérentes entre elles.

```powershell
git clone https://github.com/Alfresco/acs-deployment.git
cd acs-deployment\docker-compose
docker compose -f community-compose.yaml up -d     # 1re fois : téléchargement long des images
docker compose -f community-compose.yaml ps        # tous les services doivent être "running"
docker compose -f community-compose.yaml logs -f alfresco   # attendre la fin du démarrage du dépôt
```

### Services démarrés (à savoir expliquer)

| Service | Rôle |
|---|---|
| `alfresco` | Dépôt de contenu (Content Repository) : API REST, CMIS, règles, workflows |
| `postgres` | Base de données des métadonnées |
| `solr6` (Search Services) | Index plein texte → recherche AFTS et CMIS |
| `transform-core-aio` | Transformations : vignettes, aperçus, extraction de texte des PDF |
| `activemq` | Messagerie entre le dépôt et les transformations |
| `share` / `content-app` | Interfaces web utilisateurs |
| `proxy` | Point d'entrée unique sur le port 8080 |

### Adresses locales

| Interface | Adresse |
|---|---|
| Dépôt + administration | `http://localhost:8080/alfresco` |
| Share | `http://localhost:8080/share` |
| Content App | voir le README du dépôt officiel (l'URL a changé selon les versions) |

### Sécuriser dès le premier lancement

1. Se connecter à Share avec le compte administrateur par défaut indiqué dans la documentation Alfresco.
2. **Changer immédiatement son mot de passe** (Mon profil → Modifier le mot de passe).
3. Créer un **compte de service** dédié, par exemple `capture` (Outils d'administration → Utilisateurs), et lui donner les droits *Collaborateur* sur `Dossiers partagés/Capture-TEST` et `Capture`.
4. C'est ce compte — jamais l'admin — que CaptureLab et Postman utilisent.

---

## 4. Créer le modèle de contenu `cap` (Model Manager)

Share → Outils d'administration → **Gestionnaire de modèles** → Créer un modèle :

| Champ | Valeur |
|---|---|
| Espace de noms | `http://www.exemple.local/model/capture/1.0` |
| Préfixe | `cap` |
| Nom | `capture` |

Puis **Créer un aspect** `documentCapture` avec ces propriétés :

| Propriété | Type | Obligatoire | Indexation |
|---|---|---|---|
| `typeDocument` | d:text | oui | recherche exacte (liste : Facture, Connaissement, BonLivraison) |
| `numero` | d:text | oui | recherche exacte |
| `dateDocument` | d:date | oui | plage |
| `client` | d:text | non | plein texte |
| `montant` | d:double | non | plage |
| `sha256` | d:text | non | recherche exacte |

Enfin **Activer** le modèle. Ensuite, dans `appsettings.json` (ou `appsettings.Development.json`), mettre `"CustomAspect": "cap:documentCapture"`.

> **Aspect ou type ?** Un *type* est fixé à la création (`cm:content` → `cap:facture`). Un *aspect* s'ajoute et se retire à tout moment sur n'importe quel nœud. Pour de la capture, l'aspect est plus souple : un même document peut recevoir ses métadonnées après coup, sans migration.

---

## 5. Tester l'API avec Postman

1. Importer `postman/AlfrescoLab.postman_collection.json`.
2. Onglet **Variables** : renseigner `username` et `password` dans **Current value** seulement (jamais dans *Initial value*, qui est partagée).
3. Exécuter dans l'ordre : `01 Ticket` → `02 Découverte` → `03 Dossier` → `04 Lire` → `05 Dépôt` (choisir un fichier) → `06`… `16`.

---

## 6. Lancer CaptureLab

### Compiler et vérifier sans serveur

```powershell
cd AlfrescoLab
dotnet build
dotnet run --project tests\CaptureLab.SelfTest          # doit finir par "36 réussi(s), 0 échec(s)"
dotnet run --project src\CaptureLab.Cli -- run samples --offline   # OCR + extraction, sans Alfresco
```

### Contre Alfresco (serveur de TEST)

```powershell
$env:CAPTURELAB_ENVIRONMENT = "Development"
$env:ALFRESCO_USERNAME = "capture"
$sec = Read-Host "Mot de passe Alfresco" -AsSecureString
$env:ALFRESCO_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))

dotnet run --project src\CaptureLab.Cli -- ping
# copier les échantillons dans un dossier de travail : run déplace les fichiers traités
mkdir C:\CaptureIn; copy samples\* C:\CaptureIn
dotnet run --project src\CaptureLab.Cli -- run C:\CaptureIn
dotnet run --project src\CaptureLab.Cli -- search "TAG:\"facture\""
dotnet run --project src\CaptureLab.Cli -- cmis "SELECT cmis:name FROM cmis:document WHERE cmis:name LIKE 'Facture%'"
dotnet run --project src\CaptureLab.Cli -- watch C:\CaptureIn    # déposer un fichier : il est traité aussitôt
```

Résultat attendu de `run` :

| Fichier | Statut | Où il va |
|---|---|---|
| `facture_transmontreal.*` | Depose | `Capture-TEST/Facture/2026/10/Facture_FAC-2026147.*` |
| `connaissement_matadi.*` | Depose | `Capture-TEST/Connaissement/2026/10/…` |
| `bon_livraison_laval.*` | Depose | `Capture-TEST/BonLivraison/2026/10/…` |
| `facture_incomplete.txt` | EnException | `C:\CaptureIn\_exceptions\` (numéro, date, montant manquants) |
| `note_illisible.txt` | EnException | `C:\CaptureIn\_exceptions\` (type non reconnu) |
| Relancer le même lot | Depose (doublon détecté) | Aucun nouveau fichier dans Alfresco (409 géré) |

Un rapport CSV (ouvrable dans Excel) est écrit dans `_rapports\`.

### Passer en PRODUCTION

```powershell
$env:CAPTURELAB_ENVIRONMENT = "Production"
$env:ALFRESCO_BASEURL = "<adresse du serveur de prod>"   # jamais écrite dans le dépôt
```
`appsettings.Production.json` active l'aspect personnalisé, monte le seuil de classification à 70 % et le nombre de reprises à 5.

---

## 7. Dépannage (soutien niveau 2/3)

| Symptôme | Cause probable | Correctif |
|---|---|---|
| Conteneurs qui s'arrêtent sans erreur | Mémoire insuffisante | Allouer 16 Go à Docker, `docker stats` |
| `ping` → 502/503 pendant 2–3 min | Le dépôt démarre encore | Attendre ; CaptureLab rejoue automatiquement (1 s, 2 s, 4 s) |
| 401 Unauthorized | Ticket expiré ou mauvais en-tête | Refaire le ticket ; l'en-tête est `Basic base64(ticket)` |
| 403 Forbidden | Mauvais mot de passe, ou pas de droits sur le dossier | Vérifier le compte et les permissions du dossier |
| 404 sur `relativePath` | Chemin ou alias faux (`-shared-`, `-my-`, `-root-`) | Tester l'étape 04 dans Postman |
| 409 Conflict | Nom déjà présent dans le dossier | Normal : traité comme doublon (ou `autoRename=true`) |
| 400 sur l'aspect `cap:…` | Modèle non activé, ou préfixe faux | Model Manager → modèle **Actif** |
| Recherche vide juste après dépôt | Indexation Solr asynchrone | Attendre quelques secondes ; vérifier `solr6` |
| CMIS `cap:…` inconnu | Propriété d'aspect interrogée sans JOIN | Utiliser `JOIN cap:documentCapture` (requête 13) |
| `Impossible de lancer 'tesseract'` | Pas dans le PATH | Mettre le chemin complet de `tesseract.exe` dans `appsettings.json` |
| Accents mal lus | Langue `fra` absente | Réinstaller Tesseract avec le pack français |

Le journal de chaque document (`doc.Log`) indique l'étape exacte où il s'est arrêté : c'est la première chose à regarder en soutien.

---

## 8. Questions probables du test — réponses courtes

| Question | Réponse à donner |
|---|---|
| REST v1 ou CMIS : lequel utiliser ? | REST v1 pour tout ce qui est spécifique à Alfresco (tickets, tags, versions, aspects, recherche AFTS). CMIS quand on veut un code portable entre GED compatibles (standard OASIS) ou des requêtes de type SQL. Mon client utilise les deux. |
| Comment s'authentifier ? | `POST /tickets` puis `Authorization: Basic base64(ticket)`. Le mot de passe ne circule qu'une fois ; ticket révoqué en fin de traitement (`DELETE /tickets/-me-`). |
| Comment déposer un document avec ses métadonnées ? | `POST /nodes/{parent}/children` en multipart : `filedata`, `name`, `nodeType`, `relativePath` (crée les dossiers), propriétés comme champs du formulaire. |
| Ajouter des métadonnées métier ? | Modèle de contenu personnalisé (aspect), puis `PUT /nodes/{id}` avec `aspectNames` **complet** (il remplace la liste) et `properties`. |
| Gérer les versions ? | `PUT /nodes/{id}/content?majorVersion=…` ; historique avec `GET /nodes/{id}/versions`. Le nodeId ne change pas. |
| Éviter les doublons si on rejoue un lot ? | Nom déterministe (`Type_Numéro`), `autoRename=false`, 409 → on récupère le nœud existant. Empreinte SHA-256 stockée pour prouver l'intégrité. |
| Que faire si Alfresco est indisponible ? | Reprise exponentielle uniquement sur 408/429/502/503/504 et coupures réseau ; jamais sur 400/401/403/404/409. Le document reste dans le dossier d'entrée et part en `_erreurs` s'il échoue définitivement. |
| AFTS vs CMIS-SQL ? | AFTS : syntaxe Alfresco, `TAG:`, `PATH:`, `=cap:numero:"…"`, plein texte riche. CMIS-SQL : standard, `SELECT … FROM cmis:document JOIN cap:documentCapture`. Les deux passent par l'index Solr. |
| Séparer test et production ? | Fichiers `appsettings.{Environnement}.json`, secrets en variables d'environnement (ou coffre type Azure Key Vault), dossiers racines distincts, compte de service par environnement. |
| Pourquoi un seuil de confiance ? | Mieux vaut envoyer en validation humaine que d'archiver une donnée fausse dans la GED du client. |

### Faire le lien avec TotalAgility (à réviser — je ne l'ai pas encore utilisé)

| Concept de ce labo | Équivalent probable dans Tungsten TotalAgility | À confirmer |
|---|---|---|
| Dossier surveillé / lot | Import source, création de lot (Capture) | oui |
| `KeywordClassifier` + seuil | Classification (projet de classification, apprentissage sur échantillons) | oui |
| `RegexExtractor` | Extraction (localisateurs de format, zones, groupes de champs) | oui |
| `BusinessValidator` + `_exceptions` | Activité de validation humaine (Validation / Verification) | oui |
| `AlfrescoExporter` | Activité d'export / connecteur vers la GED (OnBase, SharePoint…) | oui |
| Code C# sur mesure | Activités de script .NET et appels REST/SOAP dans un processus BPM | oui |
| `doc.Log` + statuts | Suivi des tâches et des lots dans la console de travail | oui |

### Présentation orale (1 minute)

> « En 2023, un client du secteur du transport voulait une solution de capture, mais la licence Ephesoft était trop chère pour lui. J'ai conçu une chaîne sur mesure : numérisation et OCR, indexation des métadonnées, puis dépôt dans Alfresco Community par une bibliothèque C# qui utilisait l'API REST et CMIS, avec des serveurs de test et de production séparés et des tests Postman. Pour préparer ce poste, je l'ai réécrite entièrement en .NET 8 : classification avec seuil de confiance, extraction et validation métier, file d'exceptions, reprise automatique et détection des doublons. Ce sont les mêmes étapes qu'on retrouve dans TotalAgility, que je suis en train d'apprendre. »

---

## 9. Exercices pour aller plus loin

1. Ajouter l'OCR des PDF : convertir chaque page en image (ex. Ghostscript) avant `IOcrEngine`.
2. Remplacer Tesseract par Azure AI Document Intelligence en implémentant `IOcrEngine` (une seule classe à écrire).
3. Créer une **règle de dossier** Alfresco sur `Capture/Facture` qui déclenche une action (copie, notification).
4. Écrire une requête AFTS par chemin : `PATH:"/app:company_home/app:shared/cm:Capture//*" AND =cap:typeDocument:"Facture"`.
5. Écrire les métadonnées extraites aussi dans une table SQL Server (intégration ERP).
