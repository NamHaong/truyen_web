using Npgsql;

namespace WebDocTruyen.Models
{
    public class BinhLuanLikeStore
    {
        public static bool DaLike(int maNguoiDung, int maBinhLuan)
        {
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("SELECT 1 FROM \"BinhLuanLike\" WHERE \"MaNguoiDung\"=@u AND \"MaBinhLuan\"=@b", c);
            cmd.Parameters.AddWithValue("u", maNguoiDung);
            cmd.Parameters.AddWithValue("b", maBinhLuan);
            return cmd.ExecuteScalar() != null;
        }

        public static (bool daLike, int tongLike) ToggleLike(int maNguoiDung, int maBinhLuan)
        {
            using var c = StoreDb.Open();
            bool daLike;
            using (var del = new NpgsqlCommand("DELETE FROM \"BinhLuanLike\" WHERE \"MaNguoiDung\"=@u AND \"MaBinhLuan\"=@b", c))
            {
                del.Parameters.AddWithValue("u", maNguoiDung);
                del.Parameters.AddWithValue("b", maBinhLuan);
                daLike = del.ExecuteNonQuery() == 0;
            }
            if (daLike)
            {
                using var ins = new NpgsqlCommand("INSERT INTO \"BinhLuanLike\" (\"MaBinhLuan\",\"MaNguoiDung\") VALUES (@b,@u) ON CONFLICT DO NOTHING", c);
                ins.Parameters.AddWithValue("u", maNguoiDung);
                ins.Parameters.AddWithValue("b", maBinhLuan);
                ins.ExecuteNonQuery();
            }
            return (daLike, DemLike(maBinhLuan));
        }

        public static int DemLike(int maBinhLuan)
        {
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM \"BinhLuanLike\" WHERE \"MaBinhLuan\"=@b", c);
            cmd.Parameters.AddWithValue("b", maBinhLuan);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static Dictionary<int, int> DemTatCaLike()
        {
            var d = new Dictionary<int, int>();
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("SELECT \"MaBinhLuan\", COUNT(*) FROM \"BinhLuanLike\" GROUP BY \"MaBinhLuan\"", c);
            using var r = cmd.ExecuteReader();
            while (r.Read()) d[r.GetInt32(0)] = (int)r.GetInt64(1);
            return d;
        }

        public static HashSet<int> DanhSachDaLikeCuaUser(int maNguoiDung)
        {
            var set = new HashSet<int>();
            using var c = StoreDb.Open();
            using var cmd = new NpgsqlCommand("SELECT \"MaBinhLuan\" FROM \"BinhLuanLike\" WHERE \"MaNguoiDung\"=@u", c);
            cmd.Parameters.AddWithValue("u", maNguoiDung);
            using var r = cmd.ExecuteReader();
            while (r.Read()) set.Add(r.GetInt32(0));
            return set;
        }
    }
}
