# 🍣 Velkommen til **SushiCounter**
### *"Strava, men for Sushi"*
###### *-Troværdige kilder*

For at bruge denne applikation skal du bruge en **.env**-fil og have det rigtige DB setup.

---

## ⚙️ .Env guide

### 📝 .Env how?
Du skal hente GitHub-projektet ned på din IDE og tilføje en  
tom tekstfil, der **KUN** hedder **.env**

---

### 📁 .Env hvor?
Filen skal placeres inde i **backend**-mappen  
👉 [Link her][BackendURL]

---

### 🧩 .Env indhold?
Du skal i din **.env**-fil have følgende indhold:

```csharp
MONGO_CONNECTION_STRING=mongodb+srv://<BRUGERNAVN>:<KODE>@<DITCLUSTERNAVN>/?retryWrites=true&w=majority&appName=Cluster0

MONGO_DATABASE_NAME=DBNAVN

```
---

### 🔒 .Env unik?
*"&lt;BRUGERNAVN&gt;"* skal være navnet på din profil på MongoDB Atlas
*"&lt;KODE&gt;"* skal være din kode til den profil
*"&lt;DITCLUSTERNAVN&gt;"* skal være dit cluster-navn

---

### 🗂️ Collections (MONGODB)
Du skal have to collections for at dette projekt kan fungere på din IDE:

1. Users
2. Sessions

---

### 🍱 Hvad så nu?
Nu er der ikke så meget andet at sige end:
<Strong>Bon appétit!</strong> 🍣


