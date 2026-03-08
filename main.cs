using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Poses;
using MelonLoader;
using MelonLoader.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using RumbleModUI;
using System.Linq;
using System.Collections;
using Il2CppRUMBLE.Managers;
using RumbleModdingAPI.RMAPI;
using UnityEngine;

namespace RumbleStats
{
    public class StatsMod
    {
        public string ModName { get; set; }
        private string baseFilePath;
        public bool storeEntryTime { get; set; } = false;

        public bool DebugMode { get; set; } = false;

        public Dictionary<string, Dictionary<string, Type>> columnTypes = new Dictionary<string, Dictionary<string, Type>>();

        public event Action StatAdded;

        private void Log(string message)
        {
            if (DebugMode)
            {
                MelonLogger.Msg(message);
            }
        }

        public void InitializeStatFile(string fileName, Dictionary<string, Type> columns)
        {
            MelonLogger.Msg($"Initializing stat file: {fileName}");
            if (string.IsNullOrEmpty(fileName))
                MelonLogger.Error("File name cannot be null or empty.", nameof(fileName));
                
            if (columns == null || columns.Count == 0)
                MelonLogger.Error("Columns cannot be null or empty.", nameof(columns));

            baseFilePath = Path.Combine(MelonEnvironment.UserDataDirectory, "RUMBLEStats", ModName);
            string csvFilePath = Path.Combine(baseFilePath, $"{fileName}.csv");

            Log($"Base file path: {baseFilePath}");
            Log($"CSV file path: {csvFilePath}");

            var finalColumns = new Dictionary<string, Type>(columns);
            if (storeEntryTime)
            {
                finalColumns["EntryTime"] = typeof(DateTime);
                Log("Added 'EntryTime' column.");
            }

            columnTypes[$"{fileName}.csv"] = finalColumns;

            string columnNames = string.Join(",", finalColumns.Keys);
            Log($"Columns: {columnNames}");

            try
            {
                if (!Directory.Exists(baseFilePath))
                {
                    Directory.CreateDirectory(baseFilePath);
                    MelonLogger.Msg($"Created directory: {baseFilePath}");
                }

                if (!File.Exists(csvFilePath))
                {
                    using (StreamWriter writer = new StreamWriter(csvFilePath, false))
                    {
                        writer.WriteLine(columnNames);
                        MelonLogger.Msg($"Initialized stat file: {csvFilePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize stat file: {ex.Message}");
            }
        }

        public bool StatFileExists(string csvFileName)
        {
            string path = Path.Combine(baseFilePath, $"{csvFileName}.csv");
            bool exists = File.Exists(path);
            Log($"Checking if stat file exists: {path} - Exists: {exists}");
            return exists;
        }

        public List<string[]> GetAllRows(string csvFileName, bool skipHeader = false)
        {
            string path = Path.Combine(baseFilePath, $"{csvFileName}.csv");
            Log($"Reading all rows from file: {path}");

            if (!StatFileExists(csvFileName))
                MelonLogger.Error($"Stat file '{path}' is not initialized or does not exist. Ensure you have called InitializeStatFile before reading rows.");  

            var rows = new List<string[]>();

            using (var reader = new StreamReader(path))
            {
                if (skipHeader)
                {
                    string header = reader.ReadLine();
                    Log($"Skipped header: {header}");
                }

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    rows.Add(line.Split(','));
                    Log($"Read row: {line}");
                }
            }

            Log($"Total rows read: {rows.Count}");
            return rows;
        }

        public List<string[]> GetRows(Func<string[], bool> predicate, string csvFileName)
        {
            Log($"Getting rows with predicate from file: {csvFileName}");
            var rows = GetAllRows(csvFileName);
            var filteredRows = rows.FindAll(predicate.Invoke);
            Log($"Total rows matching predicate: {filteredRows.Count}");
            return filteredRows;
        }

        public string[] GetRow(int index, string csvFileName)
        {
            Log($"Getting row at index {index} from file: {csvFileName}");
            var rows = GetAllRows(csvFileName);

            if (index < 0 || index >= rows.Count)
            {
                MelonLogger.Error(nameof(index), $"Index {index} is out of range. Total rows: {rows.Count}");
                return null;
            }

            var matchingRow = rows[index];
            Log($"Found row at index {index}: {string.Join(",", matchingRow)}");
            return matchingRow;
        }

        public void RemoveRows(Func<string[], bool> predicate, string csvFileName)
        {
            Log($"Removing rows with predicate from file: {csvFileName}");
            var rows = GetAllRows(csvFileName);
            var filteredRows = rows.Where(row => !predicate(row)).ToList();

            string path = Path.Combine(baseFilePath, $"{csvFileName}.csv");
            File.WriteAllLines(path, filteredRows.Select(row => string.Join(",", row)));
        }

        public void RemoveRow(int index, string csvFileName)
        {
            Log($"Removing row at index {index} from file: {csvFileName}");
            var rows = GetAllRows(csvFileName);

            if (index < 0 || index >= rows.Count)
            {
                MelonLogger.Error(nameof(index), $"Index {index} is out of range. Total rows: {rows.Count}");
                return;
            }

            rows.RemoveAt(index);

            WriteAllRows(csvFileName, rows);
            Log($"Row at index {index} removed successfully.");
        }

        public void WriteAllRows(string csvFileName, List<string[]> rows)
        {
            string path = Path.Combine(baseFilePath, $"{csvFileName}.csv");
            Log($"Writing all rows to file: {path}");

            if (!columnTypes.ContainsKey($"{csvFileName}.csv"))
            {
                MelonLogger.Error($"The stat file '{csvFileName}.csv' has not been initialized.");
                return;
            }

            var columns = columnTypes[$"{csvFileName}.csv"];
            string header = string.Join(",", columns.Keys);

            try
            {
                using (var writer = new StreamWriter(path, false))
                {
                    writer.WriteLine(header);
                    Log($"Wrote header: {header}");

                    foreach (var row in rows)
                    {
                        writer.WriteLine(string.Join(",", row));
                        Log($"Wrote row: {string.Join(",", row)}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to write rows to file: {csvFileName}. Error: {ex.Message}");
            }
        }

        public void UpdateRow(int index, string[] updatedValues, string csvFileName)
        {
            Log($"Updating row at index {index} in file: {csvFileName}");
            string path = Path.Combine(baseFilePath, $"{csvFileName}.csv");
            var rows = GetAllRows(csvFileName);

            if (index < 0 || index >= rows.Count)
            {
                MelonLogger.Error($"Index {index} is out of range. Total rows: {rows.Count}");
                return;
            }

            rows[index] = updatedValues;
            try
            {
                WriteAllRows(csvFileName, rows);
                Log($"Row updated successfully at index {index} in file: {csvFileName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to update row at index {index}. Error: {ex.Message}");
            }
        }

        public void AddStatRow(object[] values, string csvFileName)
        {
            Log($"Adding row to file: {csvFileName}");
            if (!columnTypes.ContainsKey($"{csvFileName}.csv"))
                MelonLogger.Error($"The stat file '{$"{csvFileName}.csv"}' has not been initialized. Ensure you have called InitializeStatFile before adding rows.");

            var columns = columnTypes[$"{csvFileName}.csv"];
            int expectedColumnCount = storeEntryTime ? columns.Count - 1 : columns.Count;

            Log($"Expected column count: {expectedColumnCount}, Values provided: {values.Length}");
            if (values.Length != expectedColumnCount)
                MelonLogger.Error($"The number of values ({values.Length}) does not match the number of columns ({expectedColumnCount}).");

            var row = new string[columns.Count];
            int index = 0;

            foreach (var column in columns)
            {
                if (storeEntryTime && column.Key == "EntryTime")
                {
                    row[index++] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                    Log($"Added 'EntryTime': {row[index - 1]}");
                    continue;
                }

                if (index < values.Length)
                {
                    var value = values[index];
                    if (value != null && value.GetType() != column.Value)
                        MelonLogger.Error($"Value at column '{column.Key}' is not of type {column.Value.Name}. Provided type: {value.GetType().Name}");

                    row[index++] = FormatValue(value);
                    Log($"Formatted value for column '{column.Key}': {row[index - 1]}");
                }
            }

            WriteStatRow(row, csvFileName);
            Log($"Row added successfully: {string.Join(",", row)}");

            StatAdded?.Invoke();
        }

        private void WriteStatRow(string[] row, string csvFileName)
        {
            string path = Path.Combine(baseFilePath, $"{csvFileName}.csv");
            Log($"Writing row to file: {path}");

            var columns = columnTypes[$"{csvFileName}.csv"];
            if (row.Length != columns.Count)
                MelonLogger.Error($"The number of values ({row.Length}) does not match the number of columns ({columns.Count}).");

            try
            {
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    string rowString = string.Join(",", row);
                    writer.WriteLine(rowString);
                    Log($"Row written to file: {rowString}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to write row to stat file: {ex.Message}");
            }
        }

        public string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is int i) return i.ToString(CultureInfo.InvariantCulture);
            if (value is float f) return f.ToString(CultureInfo.InvariantCulture);
            if (value is double d) return d.ToString(CultureInfo.InvariantCulture);
            if (value is bool b) return b ? "true" : "false";
            if (value is DateTime dt) return dt.ToString("o", CultureInfo.InvariantCulture);
            if (value is TimeSpan ts) return ts.ToString("c", CultureInfo.InvariantCulture);
            if (value is string s) return s;

            try
            {
                return value.ToString();
            }
            catch
            {
                MelonLogger.Error($"Unsupported value type: {value?.GetType()?.FullName ?? "null"}");
                return "unsupported";
            }
        }
    }

    public class Main : MelonMod
    {
        public static Main instance;

        StatsMod mod = new StatsMod();

        List<StatsMod> statsMods = new List<StatsMod>();

        Mod UImod = new Mod();
        private UI UI = UI.instance;

        public Main()
        {
            instance = this;
        }

        private bool init = false;
        public string currentScene = "Loader";

        public enum RoundResult
        {
            T,
            W,
            L,
            u
        }
        private int round = 0;
        private RoundResult[] rounds = new RoundResult[3] { RoundResult.u, RoundResult.u, RoundResult.u };

        public List<string> roundPoseSets = new List<string>();

        public override void OnLateInitializeMelon()
        {
            Actions.onMatchEnded += onMatchEnded;
            Actions.onRoundEnded += () =>
            {
                MelonCoroutines.Start(OnRoundEnded());
            };
            Actions.onMatchStarted += onMatchStarted;

            mod.ModName = "RUMBLEStats";
            mod.storeEntryTime = true;

            var structureColumns = new Dictionary<string, Type>
            {
                { "Structure Name", typeof(string) },
                { "Modifier Name", typeof(string) }
            };

            mod.InitializeStatFile("MoveData", structureColumns);
            RumbleStats.Main.instance.RegisterCSVFile("MoveData", "Records each move or modifier you do, even if it does not activate on a structure.");

            var healthColumns = new Dictionary<string, Type>
            {
                { "Opponent Name", typeof(string) },
                { "Your Health", typeof(int) },
                { "Opponent Health", typeof(int) },
                { "Round Count", typeof(int) }
            };

            mod.InitializeStatFile("RoundHealthData", healthColumns);
            RumbleStats.Main.instance.RegisterCSVFile("RoundHealthData", "Records the data of your health and the opponents health at the end of each round.");

            var matchColumns = new Dictionary<string, Type>
            {
                { "Opponent Name", typeof(string) },
                { "Match Result", typeof(string) },
                { "Rounds", typeof(string) },
                { "BP Gained", typeof(int) },
                { "Total BP", typeof(int) },
                { "Map", typeof(string) }
            };

            mod.InitializeStatFile("MatchData", matchColumns);
            RumbleStats.Main.instance.RegisterCSVFile("MatchData", "Records the data of each map such as the map, bp gained, the match result (win or loss), rounds (WLW, WLL, LL-, etc).");

            UImod.ModName = "RumbleStats";
            UImod.ModVersion = "1.0.4";

            UImod.SetFolder("RUMBLEStats");
            UImod.AddDescription("Description", "", "A mod that allows other mods to track statistics in the form of CSV files. ModUI toggles currently do not work.", new Tags { IsSummary = true });

            UImod.GetFromFile();
            UI.instance.UI_Initialized += OnUIInit;

            MelonLogger.Msg("RumbleStats initiated");
        }

        private void OnUIInit()
        {
            UI.AddMod(UImod);
        }

        public void RegisterCSVFile(string fileName, string description)
        {
            ModSetting<bool> toggle = UImod.AddToList(fileName, true, 0, description, new Tags());
        }

        [HarmonyPatch(typeof(PlayerPoseSystem), "OnPoseSetCompleted")]
        public class PosePatch
        {
            static void Prefix(ref PoseSet set)
            {
                Main _instance = RumbleStats.Main.instance;

                if (_instance.currentScene == "Map0" || _instance.currentScene == "Map1")
                {
                    _instance.roundPoseSets.Add(set.name);
                }
            }
        }

        private void onMatchEnded()
        {
            string roundsResult = string.Concat(rounds.Select(result => result == RoundResult.u ? "-" : result.ToString()));
            string matchResult = string.Empty;
            string map = currentScene == "Map0" ? "Ring" : "Pit";
            int bpGained = 2;
            int totalBp = Calls.Players.GetLocalPlayer().Data.GeneralData.BattlePoints;
            int winCount = 0;
            int loseCount = 0;
            foreach (RoundResult result in rounds)
            {
                if (result == RoundResult.W)
                {
                    winCount++;
                }
                else if (result == RoundResult.L)
                {
                    loseCount++;
                }
            }

            if (winCount >= 2)
            {
                matchResult = "Win";
                bpGained = 5;
            } else
            {
                matchResult = "Lose";
            }

            mod.AddStatRow(new object[]
            {
                PlayerManager.instance.AllPlayers.ToArray()[1].Data.GeneralData.PublicUsername,
                matchResult,
                roundsResult,
                bpGained,
                totalBp,
                map
            }, "MatchData");
        }

        private void onMatchStarted()
        {
            round = 0;
        }

        private IEnumerator OnRoundEnded()
        {
            int localHealth = PlayerManager.instance.LocalPlayer.Data.HealthPoints;
            int remoteHealth = PlayerManager.instance.AllPlayers.ToArray()[1].Data.HealthPoints;

            mod.AddStatRow(new object[]
            {
                PlayerManager.instance.AllPlayers.ToArray()[1].Data.GeneralData.PublicUsername,
                localHealth,
                remoteHealth,
                round + 1
            }, "RoundHealthData");

            if (localHealth == remoteHealth)
            {
                rounds[round] = RoundResult.T;
            } else if (localHealth > remoteHealth)
            {
                rounds[round] = RoundResult.W;
            } else
            {
                rounds[round] = RoundResult.L;
            }

            yield return new WaitForSeconds(1);

            AddToFile(roundPoseSets);
            roundPoseSets.Clear();

            round++;
        }

        private void AddToFile(List<string> poseNames)
        {
            List<string> structures = new List<string>
            {
                "PoseSetDisc",
                "PoseSetSpawnPillar",
                "PoseSetBall",
                "PoseSetWall_Grounded",
                "PoseSetSpawnCube"
            };

            foreach (string poseName in poseNames)
            {
                if (structures.Contains(poseName))
                {
                    mod.AddStatRow(new object[]
                    {
                    poseName,
                    string.Empty
                    }, "MoveData");
                }
                else
                {
                    mod.AddStatRow(new object[]
                    {
                    string.Empty,
                    poseName
                    }, "MoveData");
                }
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            currentScene = sceneName;
            init = false;
        }
    }
}
