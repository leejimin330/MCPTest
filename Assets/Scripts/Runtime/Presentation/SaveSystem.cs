using System;
using System.IO;
using FarmCreature.Core;
using FarmCreature.Data;
using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>세이브 파일 읽기/쓰기만 담당한다. 변환 로직은 전부 SaveSerializer 에 있다.</summary>
    public static class SaveSystem
    {
        public const string FileName = "save.json";

        public static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static bool Save(GameState state, out string error)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("o");
                string json = SaveSerializer.ToJson(SaveSerializer.Capture(state, stamp));
                File.WriteAllText(SavePath, json);
                error = null;
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>세이브가 없거나 손상됐으면 false. 호출자는 새 게임으로 시작한다.</summary>
        public static bool TryLoad(
            SpeciesDatabase speciesDatabase,
            ItemDatabase itemDatabase,
            out GameState state,
            out string error)
        {
            state = null;

            if (!File.Exists(SavePath))
            {
                error = null;
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(SavePath);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }

            return SaveSerializer.TryRestore(
                json, speciesDatabase, itemDatabase, out state, out error);
        }
    }
}
