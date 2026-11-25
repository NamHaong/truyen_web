using Npgsql;

namespace WebDocTruyen.Models
{
    public class ThichTruyenStore
    {
        public static bool DaThich(int maNguoiDung, int maTruyen)
        {
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("SELECT 1 FROM \"ThichTruyen\" WHERE \"MaNguoiDung\"=@u AND \"MaTruyen\"=@t", c);
            cmd.Parameters.AddWithValue("u", maNguoiDung);
            cmd.Parameters.AddWithValue("t", maTruyen);
            return cmd.ExecuteScalar() != null;
        }

        public static bool ToggleThich(int maNguoiDung, int maTruyen)
        {
            using var c = StoreDb.Open();
            using (var del = new NpgsqlCommand("DELETE FROM \"ThichTruyen\" WHERE \"MaNguoiDung\"=@u AND \"MaTruyen\"=@t", c))
            {
                del.Parameters.AddWithValue("u", maNguoiDung);
                del.Parameters.AddWithValue("t", maTruyen);
                if (del.ExecuteNonQuery() > 0) return false;
            }
            using var ins = new NpgsqlCommand("INSERT INTO \"ThichTruyen\" (\"MaNguoiDung\",\"MaTruyen\") VALUES (@u,@t) ON CONFLICT DO NOTHING", c);
            ins.Parameters.AddWithValue("u", maNguoiDung);
            ins.Parameters.AddWithValue("t", maTruyen);
            ins.ExecuteNonQuery();
            return true;
        }

        public static int DemSoThich(int maTruyen, int baseCount = 0)
        {
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"ThichTruyen\" WHERE \"MaTruyen\"=@t", c);
            cmd.Parameters.AddWithValue("t", maTruyen);
            return Math.Max(baseCount, Convert.ToInt32(cmd.ExecuteScalar()));
        }
    }
}
