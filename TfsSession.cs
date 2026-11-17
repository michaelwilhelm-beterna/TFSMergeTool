using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;

namespace MergeHelper
{
    /// <summary>Verbindung zu einer Azure-DevOps-Collection inkl. TFVC-Operationen.</summary>
    public sealed class TfsSession : IDisposable
    {
        private readonly TfsTeamProjectCollection _tpc;
        private readonly VersionControlServer _vcs;

        public DateTime CreatedUtc { get; } = DateTime.UtcNow;

        private TfsSession(TfsTeamProjectCollection tpc)
        {
            _tpc = tpc;
            _vcs = tpc.GetService<VersionControlServer>();
        }

        public static async Task<TfsSession> OpenAsync(ConnectionConfig c, IntPtr parentWindow)
        {
            if (!Uri.TryCreate(c.Url?.Trim(), UriKind.Absolute, out var uri))
                throw new InvalidOperationException("Ungültige URL: " + c.Url);

            VssCredentials creds;
            if (c.AuthMode == AuthMode.Pat)
            {
                var pat = ConfigStore.Unprotect(c.PatEncrypted);
                if (string.IsNullOrEmpty(pat))
                    throw new InvalidOperationException("Für diese Verbindung ist kein PAT hinterlegt.");
                creds = new VssCredentials(new VssBasicCredential(string.Empty, pat));
            }
            else
            {
                var token = await AuthService.GetTokenAsync(c.TenantId, parentWindow);
                creds = new VssCredentials(new VssAadCredential(new VssAadToken("Bearer", token)));
            }

            return await Task.Run(() =>
            {
                var tpc = new TfsTeamProjectCollection(uri, creds);
                tpc.Authenticate();
                return new TfsSession(tpc);
            });
        }

        public string AuthorizedUser => _vcs.AuthorizedUser;

        public List<BranchInfo> GetBranches()
        {
            var objects = _vcs.QueryRootBranchObjects(RecursionType.Full);
            return objects
                .Where(b => !b.Properties.RootItem.IsDeleted)
                .Select(b => new BranchInfo
                {
                    Path = b.Properties.RootItem.Item,
                    Parent = b.Properties.ParentBranch?.Item,
                    Children = (b.ChildBranches ?? Array.Empty<ItemIdentifier>())
                        .Where(ch => !ch.IsDeleted)
                        .Select(ch => ch.Item).ToList()
                })
                .GroupBy(b => b.Path, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(b => b.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public List<ChangesetRow> GetMergeCandidates(string source, string target)
        {
            var candidates = _vcs.GetMergeCandidates(source, target, RecursionType.Full);
            return candidates.Select(mc => mc.Changeset).Select(cs => new ChangesetRow
            {
                Id = cs.ChangesetId,
                User = string.IsNullOrEmpty(cs.OwnerDisplayName) ? cs.Owner : cs.OwnerDisplayName,
                Date = cs.CreationDate,
                Comment = cs.Comment ?? "",
                CommentSingleLine = OneLine(cs.Comment)
            }).ToList();
        }

        public (ChangesetRow Info, List<ChangeRow> Changes) GetChangesetDetails(int id)
        {
            var cs = _vcs.GetChangeset(id, true, false);
            var info = new ChangesetRow
            {
                Id = cs.ChangesetId,
                User = string.IsNullOrEmpty(cs.OwnerDisplayName) ? cs.Owner : cs.OwnerDisplayName,
                Date = cs.CreationDate,
                Comment = cs.Comment ?? "",
                CommentSingleLine = OneLine(cs.Comment)
            };
            var changes = (cs.Changes ?? Array.Empty<Change>())
                .Select(ch => new ChangeRow { ChangeType = ch.ChangeType.ToString(), Path = ch.Item.ServerItem })
                .OrderBy(ch => ch.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return (info, changes);
        }

        /// <summary>Sucht einen lokalen Workspace dieses Rechners, der den Zielpfad gemappt hat.</summary>
        public Workspace FindWorkspaceFor(string serverPath)
        {
            var workspaces = _vcs.QueryWorkspaces(null, _vcs.AuthorizedUser, Environment.MachineName);
            return workspaces.FirstOrDefault(w => w.IsServerPathMapped(serverPath));
        }

        /// <summary>Merged die Changesets einzeln (aufsteigend) als Cherry-Pick in den Workspace. Liefert ein Protokoll.</summary>
        public string Merge(Workspace ws, string source, string target, IEnumerable<int> changesetIds)
        {
            var sb = new StringBuilder();
            int totalConflicts = 0, totalFailures = 0;

            foreach (var id in changesetIds.OrderBy(i => i))
            {
                var version = new ChangesetVersionSpec(id);
                try
                {
                    var status = ws.Merge(source, target, version, version, LockLevel.None, RecursionType.Full, MergeOptions.None);
                    totalConflicts += status.NumConflicts;
                    totalFailures += status.NumFailures;

                    if (status.NoActionNeeded && status.NumOperations == 0)
                        sb.AppendLine($"C{id}: nichts zu tun");
                    else
                        sb.AppendLine($"C{id}: {status.NumOperations} Änderung(en), {status.NumConflicts} Konflikt(e), {status.NumFailures} Fehler");

                    foreach (var f in status.GetFailures())
                        sb.AppendLine("    ! " + f.GetFormattedMessage());
                }
                catch (Exception ex)
                {
                    totalFailures++;
                    sb.AppendLine($"C{id}: FEHLER – {ex.Message}");
                }
            }

            sb.Insert(0, $"Workspace: {ws.Name}\nKonflikte gesamt: {totalConflicts}, Fehler gesamt: {totalFailures}\n\n");
            if (totalConflicts > 0)
                sb.AppendLine("\nKonflikte bitte in Visual Studio (Team Explorer → Ausstehende Änderungen) auflösen.");
            sb.AppendLine("Einchecken erfolgt in Visual Studio.");
            return sb.ToString();
        }

        private static string OneLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return string.Join(" ⏎ ", s.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()).Where(l => l.Length > 0));
        }

        public void Dispose() => _tpc.Dispose();
    }
}
