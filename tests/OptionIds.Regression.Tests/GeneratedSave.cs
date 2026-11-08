using System;using System.Linq;using System.Collections.Generic;using TownOfHost.Roles.Core;namespace TownOfHost {public static class OptionSaver {static LogHandler logger=new();public const int Version=4;public static int SaveCalls;public static void Save(){SaveCalls++;}public static SerializableOptionsData Export()=>GenerateOptionsData();public static void Import(SerializableOptionsData d)=>LoadOptionsData(d);
private static SerializableOptionsData GenerateOptionsData()
    {
        Dictionary<int, int> singleOptions = new();
        Dictionary<int, int[]> presetOptions = new();
        Dictionary<int, Dictionary<int, int[]>> assignOptions = new();
        foreach (var option in OptionItem.AllOptions)
        {
            if (option.IsSingleValue)
            {
                if (!singleOptions.TryAdd(option.Id, option.SingleValue))
                {
                    logger.Warn($"SingleOptionのID {option.Id} が重複");
                }
            }
            else if (option is AssignOptionItem assignOptionitem)
            {
                Dictionary<int, int[]> assign = new();

                var i = 0;
                foreach (var _ in option.AllValues)
                {
                    List<int> ids = new();
                    if (assignOptionitem.RoleValues.Count <= 0)
                    {
                        assign.Add(i, []);

                        i++;
                        continue;
                    }
                    assignOptionitem.RoleValues[i].Do(role =>
                    {
                        ids.Add(role.GetRoleInfo()?.ConfigId ?? (Options.CustomRoleSpawnChances.TryGetValue(role, out var opt) ? opt.Id : -100));
                    });

                    assign.Add(i, ids.ToArray());
                    i++;
                }
                if (!assignOptions.TryAdd(option.Id, assign))
                {
                    logger.Warn($"アサインオプションの{option.Id}が重複");
                }
            }
            else if (!presetOptions.TryAdd(option.Id, option.AllValues))
            {
                logger.Warn($"プリセットオプションのID {option.Id} が重複");
            }
        }
        return new SerializableOptionsData
        {
            Version = Version,
            SingleOptions = singleOptions,
            PresetOptions = presetOptions,
            AssignOptions = assignOptions,
        };
    }
private static void LoadOptionsData(SerializableOptionsData serializableOptionsData)
    {
        if (serializableOptionsData.Version != Version)
        {
            // 今後バージョン間の移行方法を用意する場合，ここでバージョンごとの変換メソッドに振り分ける
            logger.Info($"読み込まれたオプションのバージョン {serializableOptionsData.Version} が現在のバージョン {Version} と一致しないためデフォルト値で上書きします");
            Save();
            Main.Preset1.Value = Translator.GetString("Preset_1");
            Main.Preset2.Value = Translator.GetString("Preset_2");
            Main.Preset3.Value = Translator.GetString("Preset_3");
            Main.Preset4.Value = Translator.GetString("Preset_4");
            Main.Preset5.Value = Translator.GetString("Preset_5");
            Main.Preset6.Value = Translator.GetString("Preset_6");
            Main.Preset7.Value = Translator.GetString("Preset_7");
            Main.Preset8.Value = Translator.GetString("Preset_8");
            Main.Preset9.Value = Translator.GetString("Preset_9");
            Main.Preset10.Value = Translator.GetString("Preset_10");
            Main.Preset11.Value = Translator.GetString("Preset_11");
            Main.Preset12.Value = Translator.GetString("Preset_12");
            Main.Preset13.Value = Translator.GetString("Preset_13");
            Main.Preset14.Value = Translator.GetString("Preset_14");
            Main.Preset15.Value = Translator.GetString("Preset_15");
            Main.Preset16.Value = Translator.GetString("Preset_16");
            return;
        }
        Dictionary<int, int> singleOptions = serializableOptionsData.SingleOptions;
        Dictionary<int, int[]> presetOptions = serializableOptionsData.PresetOptions;
        Dictionary<int, Dictionary<int, int[]>> assignOptions = serializableOptionsData.AssignOptions;
        // No legacy value is copied to a recovered option. Old files have no entry for it.
        foreach (var id in ShellOptionIds.RecoveredOptionIds)
        {
            if (!presetOptions.ContainsKey(id) && OptionItem.FastOptions.TryGetValue(id, out var recovered))
                recovered.SetAllValues(null); // Fill every supported preset with its definition's default.
        }
        foreach (var singleOption in singleOptions)
        {
            var id = singleOption.Key;
            var value = singleOption.Value;
            if (OptionItem.FastOptions.TryGetValue(id, out var optionItem))
            {
                optionItem.SetValue(value, doSave: false);
            }
        }
        foreach (var presetOption in presetOptions)
        {
            var id = presetOption.Key;
            var values = presetOption.Value;
            if (OptionItem.FastOptions.TryGetValue(id, out var optionItem))
            {
                optionItem.SetAllValues(values);
            }
        }
        foreach (var assignoption in assignOptions)
        {
            var id = assignoption.Key;
            var values = assignoption.Value;
            if (OptionItem.FastOptions.TryGetValue(id, out var optionItem))
            {
                if (optionItem is AssignOptionItem assignOptionItem)
                {
                    Dictionary<int, List<CustomRoles>> role = new();
                    foreach (var item in values)
                    {
                        List<CustomRoles> rolelist = new();
                        if (item.Value.Count() <= 0)
                        {
                            role.Add(item.Key, rolelist);
                            continue;
                        }
                        foreach (var roleid in item.Value)
                        {
                            // AssignOptions stores role ConfigId, which may also be a non-role detail ID.
                            var roleopt = OptionItem.AllOptions.FirstOrDefault(opt => opt.Id == roleid && opt.CustomRole != CustomRoles.NotAssigned);

                            if (roleopt is not null)
                            {
                                rolelist.Add(roleopt.CustomRole);
                            }
                            else if (roleid is -1) rolelist.Add(CustomRoles.Crewmate);
                            else if (roleid is -3) rolelist.Add(CustomRoles.Impostor);
                            else
                            {
                                var info = CustomRoleManager.AllRolesInfo.Values.FirstOrDefault(x => x.ConfigId == roleid);
                                if (info is not null)
                                {
                                    rolelist.Add(info.RoleName);
                                }
                            }
                        }
                        role.Add(item.Key, rolelist);
                    }
                    assignOptionItem.RoleValues = role;
                }
            }
        }
    }
public class SerializableOptionsData
    {
        public int Version { get; init; }
        /// <summary>プリセット外のオプション</summary>
        public Dictionary<int, int> SingleOptions { get; init; }
        /// <summary>プリセット内のオプション</summary>
        public Dictionary<int, int[]> PresetOptions { get; init; }
        /// <summary>アサインオプション</summary>
        public Dictionary<int, Dictionary<int, int[]>> AssignOptions { get; init; }
    }
}}