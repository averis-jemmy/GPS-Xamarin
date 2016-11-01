using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using SQLite;
using GPSCaptureEntity;

namespace GPSCapture
{
    public static class SqliteDatabase
    {
        private static SQLiteConnection db;

        public static SQLiteConnection newInstance(string dbPath)
        {
            db = new SQLiteConnection(dbPath);
            return db;
        }

        public static void CreateTables()
        {
            try
            {
                db.CreateTable<TPHTable>();
            }
            catch { }
        }

        public static void ClearTables()
        {
            try
            {
                db.DropTable<TPHTable>();
                db.CreateTable<TPHTable>();
            }
            catch { }
        }

        public static void ClearData()
        {
            try
            {
                db.DeleteAll<TPHTable>();
            }
            catch { }
        }

        public static List<TPHTable> CombineData(List<TphInfo> infos)
        {
            List<TPHTable> newData = new List<TPHTable>();
            var data = (from s in db.Table<TPHTable>()
                        select s).ToList();
            foreach (TphInfo info in infos)
            {
                TPHTable row = new TPHTable();
                row.ID = 0;
                row.Estate = info.Estate;
                row.Afdeling = info.Afdeling;
                row.Block = info.Block;
                row.TPH = info.TPH;
                row.Active = info.Active;
                row.Remarks = string.Empty;
                if (info.UpdatedDate.HasValue)
                {
                    row.Latitude = info.Latitude;
                    row.Longitude = info.Longitude;
                    row.Remarks = info.Remarks;
                    row.UpdatedDate = info.UpdatedDate;
                    row.IsSent = true;
                }

                newData.Add(row);
            }

            return newData;
        }

        //public static void BeginTransaction()
        //{
        //    try
        //    {
        //        db.SaveTransactionPoint();
        //    }
        //    catch { }
        //}

        public static void InsertTph(List<TPHTable> datas)
        {
            try
            {
                db.RunInTransaction(() =>
                {
                    foreach (TPHTable data in datas)
                    {
                        if (data.ID == 0)
                            db.Insert(data);
                        else
                            db.Update(data);
                    }
                });
            }
            catch { }
        }

        public static void Commit()
        {
            try
            {
                db.Commit();
            }
            catch { }
        }

        public static void UpdateTph(TPHTable model)
        {
            try
            {
                var tph = (from s in db.Table<TPHTable>()
                           where s.Estate == model.Estate && s.Afdeling == model.Afdeling &&
                           s.Block == model.Block && s.TPH == model.TPH
                           select s).FirstOrDefault();
                if (tph != null)
                {
                    if (model.Latitude == tph.Latitude && model.Longitude == tph.Longitude &&
                            model.Remarks == tph.Remarks)
                        return;
                    var temp = tph;
                    temp.Latitude = model.Latitude;
                    temp.Longitude = model.Longitude;
                    temp.Remarks = model.Remarks;
                    temp.UpdatedDate = DateTime.Now;
                    temp.IsSent = false;
                    db.Update(temp);
                }
            }
            catch { }
        }

        public static void UpdateSentStatus(TphInfo model)
        {
            try
            {
                var tph = (from s in db.Table<TPHTable>()
                           where s.Estate == model.Estate && s.Afdeling == model.Afdeling &&
                           s.Block == model.Block && s.TPH == model.TPH
                           select s).FirstOrDefault();
                if (tph != null)
                {
                    var temp = tph;
                    temp.IsSent = true;
                    db.Update(temp);
                }
            }
            catch { }
        }

        public static List<string> GetEstates()
        {
            try
            {
                var data = (from s in db.Table<TPHTable>()
                            select s).ToList();

                var estates = data.Select(s => s.Estate);
                return estates.Distinct().ToList();
            }
            catch { }

            return null;
        }

        public static List<string> GetAfdelings(string estate)
        {
            try
            {
                var data = (from s in db.Table<TPHTable>()
                            where s.Estate == estate
                            select s).ToList();

                var afdelings = data.Select(s => s.Afdeling);
                return afdelings.Distinct().ToList();
            }
            catch { }

            return null;
        }

        public static List<Block> GetBlocks(string estate, string afdeling)
        {
            try
            {
                var data = (from s in db.Table<TPHTable>()
                            where s.Estate == estate && s.Afdeling == afdeling
                            group s by s.Block into blocks
                            select new Block()
                            {
                                BlockCode = blocks.Key,
                                TotalTPH = blocks.Count()
                            });
                return data.ToList();
            }
            catch { }

            return null;
        }

        public static List<GpsCoordinate> GetTphs(string estate, string afdeling, string block)
        {
            try
            {
                var datas = (from s in db.Table<TPHTable>()
                            where s.Estate == estate && s.Afdeling == afdeling &&
                            s.Block == block && s.Active == "Y"
                            select s).ToList();

                var tphs = (from s in datas
                            orderby s.TPH.PadLeft(10, '0')
                            select new GpsCoordinate()
                            {
                                UID = s.ID,
                                Estate = s.Estate,
                                Afdeling = s.Afdeling,
                                Block = s.Block,
                                TPH = s.TPH,
                                Latitude = s.Latitude,
                                Longitude = s.Longitude,
                                Remarks = s.Remarks,
                                UpdatedDate = s.UpdatedDate
                            });
                return tphs.ToList();
            }
            catch { }

            return null;
        }

        public static List<TphInfo> GetCoordinates()
        {
            try
            {
                var data = (from s in db.Table<TPHTable>()
                            where s.UpdatedDate != null && s.IsSent == false
                            select new TphInfo()
                            {
                                Estate = s.Estate,
                                Afdeling = s.Afdeling,
                                Block = s.Block,
                                TPH = s.TPH,
                                Latitude = s.Latitude,
                                Longitude = s.Longitude,
                                Remarks = s.Remarks,
                                UpdatedDate = s.UpdatedDate
                            });
                return data.ToList();
            }
            catch { }

            return null;
        }
    }
}