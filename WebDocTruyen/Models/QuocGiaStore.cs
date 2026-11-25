using Npgsql;

namespace WebDocTruyen.Models
{
    public class QuocGiaItem
    {
        public int MaQuocGia { get; set; }
        public string TenQuocGia { get; set; } = string.Empty;
        public string? MaIso { get; set; }
        public string? MoTa { get; set; }
    }

    public static class QuocGiaStore
    {
        private const string SelectAll = "SELECT \"MaQuocGia\",\"TenQuocGia\",\"MaIso\",\"MoTa\" FROM \"QuocGia\"";

        private static QuocGiaItem Read(NpgsqlDataReader r) => new QuocGiaItem
        {
            MaQuocGia = r.GetInt32(0),
            TenQuocGia = r.GetString(1),
            MaIso = r.IsDBNull(2) ? null : r.GetString(2),
            MoTa = r.IsDBNull(3) ? null : r.GetString(3)
        };

        private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        public static List<QuocGiaItem> GetAll()
        {
            var list = new List<QuocGiaItem>();
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand(SelectAll + " ORDER BY \"MaQuocGia\"", c);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Read(r));
            return list;
        }

        public static QuocGiaItem? GetById(int id)
        {
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand(SelectAll + " WHERE \"MaQuocGia\"=@id", c);
            cmd.Parameters.AddWithValue("id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Read(r) : null;
        }

        public static void Add(QuocGiaItem item)
        {
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("INSERT INTO \"QuocGia\" (\"TenQuocGia\",\"MaIso\",\"MoTa\") VALUES (@t,@i,@m) RETURNING \"MaQuocGia\"", c);
            cmd.Parameters.AddWithValue("t", (item.TenQuocGia ?? string.Empty).Trim());
            cmd.Parameters.AddWithValue("i", (object?)Clean(item.MaIso)?.ToUpper() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("m", (object?)Clean(item.MoTa) ?? DBNull.Value);
            item.MaQuocGia = (int)cmd.ExecuteScalar()!;
        }

        public static void Update(QuocGiaItem item)
        {
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("UPDATE \"QuocGia\" SET \"TenQuocGia\"=@t,\"MaIso\"=@i,\"MoTa\"=@m WHERE \"MaQuocGia\"=@id", c);
            cmd.Parameters.AddWithValue("id", item.MaQuocGia);
            cmd.Parameters.AddWithValue("t", (item.TenQuocGia ?? string.Empty).Trim());
            cmd.Parameters.AddWithValue("i", (object?)Clean(item.MaIso)?.ToUpper() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("m", (object?)Clean(item.MoTa) ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public static void Delete(int id)
        {
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("DELETE FROM \"QuocGia\" WHERE \"MaQuocGia\"=@id", c);
            cmd.Parameters.AddWithValue("id", id);
            cmd.ExecuteNonQuery();
        }

        public static Dictionary<int, string> GetAllTruyenQuocGia()
        {
            var d = new Dictionary<int, string>();
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("SELECT \"MaTruyen\",\"QuocGia\" FROM \"TruyenQuocGia\"", c);
            using var r = cmd.ExecuteReader();
            while (r.Read()) d[r.GetInt32(0)] = r.GetString(1);
            return d;
        }

        public static void SetQuocGiaChoTruyen(int maTruyen, string? quocGia)
        {
            if (string.IsNullOrWhiteSpace(quocGia)) return;
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("INSERT INTO \"TruyenQuocGia\" (\"MaTruyen\",\"QuocGia\") VALUES (@id,@q) ON CONFLICT (\"MaTruyen\") DO UPDATE SET \"QuocGia\"=EXCLUDED.\"QuocGia\"", c);
            cmd.Parameters.AddWithValue("id", maTruyen);
            cmd.Parameters.AddWithValue("q", quocGia.Trim());
            cmd.ExecuteNonQuery();
        }

        public static string GetQuocGiaCuaTruyen(int maTruyen, string? tenTruyen, string? tacGia)
        {
            var dict = GetAllTruyenQuocGia();
            if (dict.TryGetValue(maTruyen, out var qg) && !string.IsNullOrWhiteSpace(qg))
            {
                return qg;
            }
            return DoanQuocGia(tenTruyen, tacGia);
        }

        public static string DoanQuocGia(string? tenTruyen, string? tacGia)
        {
            if (string.IsNullOrWhiteSpace(tenTruyen)) return "Trung Quốc";

            string lower = (tenTruyen + " " + (tacGia ?? "")).ToLower();
            if (lower.Contains("hà nội") || lower.Contains("sài gòn") || lower.Contains("phố cổ") || lower.Contains("triều đại") ||
                lower.Contains("đặng minh khôi") || lower.Contains("lam phương thảo"))
            {
                return "Việt Nam";
            }
            if (lower.Contains("solo leveling") || lower.Contains("manhwa") || lower.Contains("tháp") || lower.Contains("hàn quốc"))
            {
                return "Hàn Quốc";
            }
            if (lower.Contains("manga") || lower.Contains("boruto") || lower.Contains("vagabond") || lower.Contains("one piece") ||
                lower.Contains("naruto") || lower.Contains("nhật bản") || lower.Contains("isekai"))
            {
                return "Nhật Bản";
            }
            if (lower.Contains("marvel") || lower.Contains("dc") || lower.Contains("mỹ") || lower.Contains("comic"))
            {
                return "Mỹ";
            }

            return "Trung Quốc";
        }
    }
}
