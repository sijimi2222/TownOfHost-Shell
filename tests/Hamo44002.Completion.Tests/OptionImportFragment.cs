// Exact production method with game collaborator types renamed to isolated substitutes.
using System.IO;
using TownOfHost;
namespace RegressionFragments;
public class ImportNormalOptions {}
public class ImportOptions {public bool Normal=true;public bool TryCast<T>(out T value) where T:class {value=Normal?new ImportNormalOptions() as T:null;return value!=null;}}
public class ImportFactory {public ImportOptions Result=new();public bool Throw;public ImportOptions FromBytes(byte[] bytes){if(Throw)throw new FormatException();return Result;}}
public class ImportOptionsManager {public static ImportOptionsManager Instance=new();public ImportFactory gameOptionsFactory=new();public ImportOptions GameHostOptions=new(),CurrentGameOptions=new();}
public class ImportLogic {public ImportOptions Value;public int Sets,Syncs;public bool ThrowSet,ThrowSync,ThrowRollback;public void SetGameOptions(ImportOptions value){Sets++;if(ThrowSet||ThrowRollback&&Sets>1)throw new InvalidOperationException("set");Value=value;}public void SyncOptions(){Syncs++;if(ThrowSync)throw new InvalidOperationException("sync");}}
public class ImportGameManager {public static ImportGameManager Instance=new();public ImportLogic LogicOptions=new();}
public static class OptionImportFragment {
    public static void LoadVanillaOptionsString(string source)
    {
        if (source == null || source.Length > 1048576) throw new InvalidDataException("Invalid vanilla settings size.");
        var bytes = Convert.FromBase64String(source);
        var options = ImportOptionsManager.Instance.gameOptionsFactory.FromBytes(bytes);
        if (options == null || !options.TryCast<ImportNormalOptions>(out _))
            throw new InvalidDataException("Vanilla settings are missing or use an unsupported mode/version.");
        var logic = ImportGameManager.Instance?.LogicOptions;
        if (logic == null) throw new InvalidOperationException("Game options are not initialized.");
        var oldHost = ImportOptionsManager.Instance.GameHostOptions;
        var oldCurrent = ImportOptionsManager.Instance.CurrentGameOptions;
        try
        {
            ImportOptionsManager.Instance.GameHostOptions = ImportOptionsManager.Instance.CurrentGameOptions = options;
            logic.SetGameOptions(options);
            logic.SyncOptions();
        }
        catch
        {
            ImportOptionsManager.Instance.GameHostOptions = oldHost;
            ImportOptionsManager.Instance.CurrentGameOptions = oldCurrent;
            try { logic.SetGameOptions(oldCurrent); }
            catch { Logger.Warn("Vanilla settings rollback needs a manual re-sync.", "OptionSerializer"); }
            throw;
        }
    }
}
