using System;using System.Linq;using System.Collections.Generic;using TownOfHost.Roles.Core;using UnityEngine;
namespace TownOfHost { public partial class OptionItem {
public static int NumPresets=16,CurrentPreset=0;public static bool IdDuplicated;public static Dictionary<int,OptionItem> _fastOptions=new();public static List<OptionItem> _allOptions=new(),_killcooloption=new();public static IReadOnlyList<OptionItem> AllOptions=>_allOptions;public static IReadOnlyDictionary<int,OptionItem> FastOptions=>_fastOptions;
public int Id,DefaultValue;public string Name,NameColorCode,Fromtext;public TabGroup Tab;public bool IsSingleValue,HideValue,IsHeader,IsHidden,parented,InvertParentValueForDisplay;public Color NameColor;public OptionFormat ValueFormat;public CustomOptionTags Tag;public CustomOptionTags[] DisableTag;public Func<bool> IsEnabled;public Func<string> funcOptionName,Tooltip;public OptionZeroNotation ZeroNotation;public CustomRoles CustomRole,ParentRole;public List<OptionItem> Children;public OptionItem Parent;public int SingleValue;public int[] AllValues=new int[16];public int CurrentValue{get=>GetValue();set=>SetValue(value);}public Dictionary<string,string> ReplacementDictionary=new();
public static void Reset(){_fastOptions.Clear();_allOptions.Clear();_killcooloption.Clear();CurrentPreset=0;}
public virtual int GetInt()=>CurrentValue;public virtual float GetFloat()=>CurrentValue;public virtual string GetString()=>Name;public virtual string GetValueString(bool b)=>Name;public virtual string GetTextString()=>Name;public virtual bool OptionMeGetBool()=>CurrentValue!=0;protected string ApplyFormat(string s,bool coloroff=false)=>s;
public void Refresh(){}private void CallUpdateValueEvent(int a,int b){}public void SyncOptions()=>RPC.SyncCustomSettingsRPC(this);public static void SyncAllOptions(){}public class UpdateValueEventArgs:EventArgs{}public OptionItem RegisterUpdateValueEvent(EventHandler<UpdateValueEventArgs> action)=>this;
public OptionItem(int id, string name, int defaultValue, TabGroup tab, bool isSingleValue, string From = "", bool hidevalue = false)

        {

            // 必須情報の設定

            Id = id;

            Name = name;

            DefaultValue = defaultValue;

            Tab = tab;

            IsSingleValue = isSingleValue;



            // 任意情報の初期値設定

            HideValue = hidevalue;

            Fromtext = From;

            NameColor = Color.white;

            NameColorCode = "#ffffff";

            ValueFormat = OptionFormat.None;

            Tag = CustomOptionTags.All;

            DisableTag = [];

            IsHeader = false;

            IsHidden = false;

            IsEnabled = () => true;

            funcOptionName = () => "";

            ZeroNotation = OptionZeroNotation.None;

            parented = false;

            Tooltip = () => "";

            CustomRole = CustomRoles.NotAssigned;

            ParentRole = CustomRoles.NotAssigned;



            // オブジェクト初期化

            Children = new();



            // デフォルト値に設定

            if (Id == 0)

            {

                SingleValue = DefaultValue;

                CurrentPreset = SingleValue;

            }

            else if (IsSingleValue)

            {

                SingleValue = DefaultValue;

            }

            else

            {

                for (int i = 0; i < NumPresets; i++)

                {

                    AllValues[i] = DefaultValue;

                }

            }

            if (_fastOptions.TryAdd(id, this))

            {

                _allOptions.Add(this);

            }

            else

            {

#if DEBUG

                IdDuplicated = true;

#endif

                Logger.Error($"ID:{id}が重複しています name:{name} : {_fastOptions[id].Name}", "OptionItem");

            }

            if (name == "KillCooldown")

            {

                _killcooloption.Add(this);

            }

        }
public virtual int GetValue() => IsSingleValue ? SingleValue : AllValues[CurrentPreset];
public virtual bool GetBool()

        {

            if (!(CurrentValue != 0 && (Parent == null || Parent.GetBool() || CheckRoleOption(Parent))))

                return false;



            var tags = GameModeManager.GetTags(Options.CurrentGameMode);



            if (!(Tag == CustomOptionTags.All || tags.Contains(Tag)))

                return false;



            return !tags.Any(tag => DisableTag.Contains(tag));

        }
public bool CheckRoleOption(OptionItem option) => option.CustomRole is not CustomRoles.NotAssigned;
public void SetAllValues(int[] values)  // プリセット読み込み専用

        {

            AllValues = new int[NumPresets];

            for (var i = 0; i < AllValues.Length; i++)

            {

                AllValues[i] = values != null && i < values.Length ? values[i] : DefaultValue;

            }

        }
public virtual void SetValue(int afterValue, bool doSave, bool doSync = true)

        {

            int beforeValue = CurrentValue;

            if (IsSingleValue)

            {

                SingleValue = afterValue;

            }

            else

            {

                AllValues[CurrentPreset] = afterValue;

            }



            CallUpdateValueEvent(beforeValue, afterValue);

            Refresh();

            if (doSync)

            {

                SyncOptions();

            }

            if (doSave)

            {

                OptionSaver.Save();

            }

        }
public virtual void SetValue(int afterValue, bool doSync = true)

        {

            SetValue(afterValue, true, doSync);

        }
        public OptionItem Do(Action<OptionItem> action)

        {

            action(this);

            return this;

        }



        public OptionItem SetColor(Color value) => Do(i => i.NameColor = value);

        public OptionItem SetColorcode(string value) => Do(i => i.NameColorCode = value);

        public OptionItem SetValueFormat(OptionFormat value) => Do(i => i.ValueFormat = value);

        public OptionItem SetTag(CustomOptionTags value) => Do(i => i.Tag = value);

        public OptionItem SetDisableTag(CustomOptionTags[] value) => Do(i => i.DisableTag = value);

        public OptionItem SetHeader(bool value) => Do(i => i.IsHeader = value);

        public OptionItem SetCustomRole(CustomRoles role) => Do(i => i.CustomRole = role);

        public OptionItem SetHidden(bool value) => Do(i => i.IsHidden = value);

        public OptionItem SetEnabled(Func<bool> value) => Do(i => i.IsEnabled = value);

        public OptionItem SetInfo(string value) => Do(i => i.Fromtext = "<line-height=25%><size=25%>\n</size><size=60%></color> <b>" + value + "</b></size>");

        public OptionItem SetZeroNotation(OptionZeroNotation value) => Do(i => i.ZeroNotation = value);

        public OptionItem SetOptionName(Func<string> value) => Do(i => i.funcOptionName = value);

        public OptionItem SetTooltip(Func<string> value) => Do(i => i.Tooltip = value);

        public OptionItem SetSubRoleOptionItem(CustomRoles role) =>

            Do(i =>

            {

                SetParent(Options.CustomRoleSpawnChances[role]);

                SetParentRole(role);

            });



        public OptionItem SetParent(OptionItem parent, bool invertParentValueForDisplay = false) => Do(i =>

        {

            if (parented)

            {

                Logger.Warn($"{Name} : 既にSetParentがされてます", "SetParent");

                return;

            }

            if (parent == null)

            {

                // 親が見つからない(null)場合はここで止める。

                // 以前はここでparent.SetChild(i)を無条件に呼んでいたため、

                // parentがnullの時にNullReferenceExceptionでOptions.Load()全体が

                // クラッシュし、部屋作成時に真っ暗になる不具合の原因になっていた。

                Logger.Error($"{Name} : SetParentの引数がnullです(親が見つかりません)", "SetParent");

                return;

            }

            parented = true;

            i.Parent = parent;

            i.InvertParentValueForDisplay = invertParentValueForDisplay;



            // 詳細設定の多くはSetParentRoleを個別に呼んでいるが、呼び忘れた設定は

            // ParentRoleがNotAssignedのままとなり、役職ごとの設定メニュー(crlist)へ

            // 振り分けられず表示されない。親が持つ役職情報を安全に継承し、

            // 明示指定済みのParentRoleは上書きしない。

            if (i.ParentRole is CustomRoles.NotAssigned)

            {

                i.ParentRole = parent.ParentRole is not CustomRoles.NotAssigned

                    ? parent.ParentRole

                    : parent.CustomRole;

            }



            parent.SetChild(i);

        });


public OptionItem SetParentRole(CustomRoles parentrole)

            => Do(i => i.ParentRole = parentrole);
public OptionItem SetChild(OptionItem child) => Do(i => i.Children.Add(child));
} }