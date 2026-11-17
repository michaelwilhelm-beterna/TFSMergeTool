using System;
using System.Collections.Generic;

namespace MergeHelper
{
    public enum AuthMode
    {
        /// <summary>Microsoft Entra ID (Windows-Konto, SSO über WAM).</summary>
        Entra,
        /// <summary>Personal Access Token.</summary>
        Pat
    }

    public class AppConfig
    {
        public List<ConnectionConfig> Connections { get; set; } = new List<ConnectionConfig>();
        public Guid? LastConnectionId { get; set; }
        public int LastPairIndex { get; set; } = -1;
    }

    public class ConnectionConfig
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public AuthMode AuthMode { get; set; } = AuthMode.Entra;
        /// <summary>Optional: Tenant-ID oder Domain. Leer = "organizations".</summary>
        public string TenantId { get; set; } = "";
        /// <summary>PAT, mit DPAPI (CurrentUser) verschlüsselt, Base64.</summary>
        public string PatEncrypted { get; set; } = "";
        public List<BranchPair> BranchPairs { get; set; } = new List<BranchPair>();

        public ConnectionConfig CloneSettings() => new ConnectionConfig
        {
            Id = Id,
            Name = Name,
            Url = Url,
            AuthMode = AuthMode,
            TenantId = TenantId,
            PatEncrypted = PatEncrypted,
            BranchPairs = BranchPairs
        };
    }

    public class BranchPair
    {
        public string Source { get; set; } = "";
        public string Target { get; set; } = "";

        [Newtonsoft.Json.JsonIgnore]
        public string Display => $"{Source}   →   {Target}";
    }

    public class ChangesetRow
    {
        public int Id { get; set; }
        public string User { get; set; } = "";
        public DateTime Date { get; set; }
        public string Comment { get; set; } = "";
        public string CommentSingleLine { get; set; } = "";
        public string DateText => Date.ToString("dd.MM.yyyy HH:mm");
    }

    public class ChangeRow
    {
        public string ChangeType { get; set; } = "";
        public string Path { get; set; } = "";
    }

    public class BranchInfo
    {
        public string Path { get; set; } = "";
        public string Parent { get; set; }
        public List<string> Children { get; set; } = new List<string>();
        public override string ToString() => Path;
    }
}
