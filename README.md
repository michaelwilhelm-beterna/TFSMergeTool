# MergeHelper

Kleines WPF-Tool (.NET Framework 4.8) zum Anzeigen, Filtern und Mergen von TFVC-Merge-Kandidaten aus Azure DevOps.

## Bauen / Starten

```
dotnet build -c Release
bin\Release\net48\MergeHelper.exe
```

## Bedienung

1. **Verbindung anlegen** (oben links): Name, Collection-URL (`https://dev.azure.com/<organisation>`), Anmeldung
   - *Windows-/Microsoft-Konto*: SSO über den Windows-Kontomanager, ggf. einmalig Anmeldedialog.
     Tenant nur angeben, wenn die Organisation in einem fremden Tenant liegt.
   - *PAT*: Scope **Code (Read & Write)**; wird per Windows-DPAPI verschlüsselt gespeichert.
2. **Branch-Paar anlegen** (oben rechts): Quelle/Ziel aus der Branch-Liste des Servers wählen.
3. **Changesets laden** (Button oder Doppelklick auf das Paar).
4. Im Grid: Spaltenkopf klicken = sortieren, Textfeld im Kopf = filtern.
   - Text: alle Wörter müssen vorkommen, `!wort` schließt aus.
   - Changeset/Datum: `>`, `>=`, `<`, `<=`, `=` – z.B. `>=12000`, `>01.10.2026 <01.11.2026`.
5. Markierte Zeilen: **Nummern kopieren**, **Details…** (geänderte Dateien, auch Doppelklick), **Mergen…**.

### Mergen
Die markierten Changesets werden einzeln und aufsteigend (Cherry-Pick) in den lokalen Workspace gemergt,
der den Ziel-Branch auf diesem Rechner gemappt hat. Es entstehen nur ausstehende Änderungen –
Konflikte auflösen und Einchecken erfolgt wie gewohnt in Visual Studio.

## Konfiguration
`%AppData%\MergeHelper\config.json`
