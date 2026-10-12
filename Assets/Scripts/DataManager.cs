using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;
using System.Linq;
// using Dragon.SDK;

public static class DataManager
{
    private static readonly StringBuilder tempString = new();
    public const string LOBBY_SCENE = "Lobby";
    public const string GAME_PLAY_SCENE = "GamePlay";

    public const bool isPlaying = false;

    #region LEVEL
    private const string LEVEL = "level";
    private const int LOOP_START_LEVEL = 30;
    private const int MAX_LEVEL = 1000;

    public static int GetLevel() => PlayerPrefs.GetInt(LEVEL, 1);

    public static void SetLevel(int value) => PlayerPrefs.SetInt(LEVEL, value);

    public static void NextLevel()
    {
        SetLevel(GetLevel() + 1);
    }

    public static int GetRealLevel()
    {
        int storedLevel = GetLevel();
        var maxLevel = GameController.instance != null ? GameController.instance.GameConfig.MaxLevel : MAX_LEVEL;
        if (storedLevel <= maxLevel) return storedLevel;
        int loopCount = maxLevel - LOOP_START_LEVEL + 1;
        return LOOP_START_LEVEL + ((storedLevel - maxLevel - 1) % loopCount);
    }

    public static int GetNextRealLevel()
    {
        int storedLevel = GetLevel() + 1;
        var maxLevel = GameController.instance != null ? GameController.instance.GameConfig.MaxLevel : MAX_LEVEL;
        if (storedLevel <= maxLevel) return storedLevel;
        int loopCount = maxLevel - LOOP_START_LEVEL + 1;
        return LOOP_START_LEVEL + ((storedLevel - maxLevel - 1) % loopCount);
    }

    public static int GetRealLevelSpecifically(int level)
    {
        int storedLevel = level;
        var maxLevel = GameController.instance != null ? GameController.instance.GameConfig.MaxLevel : MAX_LEVEL;
        if (storedLevel <= maxLevel) return storedLevel;
        int loopCount = maxLevel - LOOP_START_LEVEL + 1;
        return LOOP_START_LEVEL + ((storedLevel - maxLevel - 1) % loopCount);
    }
//     public static void LoadScene(SceneType sceneType, bool isWait = false)
// {
//     var pop = PopupLoading.Show();
//     pop.SetSceneName(sceneType, isWait);
// }

    #endregion

    #region SETTING
    public const string MuteSound = "IsMuteSound";
    public const string MuteMusic = "IsMuteMusic";
    public const string MuteVib = "IsMuteVib";
    public static bool IsMuteSound
    {
        get { return PlayerPrefs.GetInt(MuteSound, 0) == 1; }
        set
        {
         //   SoundManager.Instance.SetSFXVolume(!value);
            PlayerPrefs.SetInt(MuteSound, value ? 1 : 0);
        }
    }

    public static bool IsMuteMusic
    {
        get { return PlayerPrefs.GetInt(MuteMusic, 1) == 1; }
        set
        {
        //    SoundManager.Instance.SetBGMVolume(!value);
            PlayerPrefs.SetInt(MuteMusic, value ? 1 : 0);
        }
    }

    public static bool IsMuteVib
    {
        get { return PlayerPrefs.GetInt(MuteVib, 0) == 1; }
        set
        {
            PlayerPrefs.SetInt(MuteVib, value ? 1 : 0);
        }
    }
    #endregion

    #region HEART
    private const string MaxHeart = "MaxHeart";
    private const string LastTimeLoseHeart = "LastTimeLoseHeart";
    private const string TimeInfinityHeart = "TimeInfinityHeart";
    private const string HEART = "heart";
    private static int _maxHeart = 5;
    public static int GetMaxHeart() => PlayerPrefs.GetInt(MaxHeart, 5);
    public static void SetMaxHeart(int value)
    {
        PlayerPrefs.SetInt(MaxHeart, value);
        _maxHeart = value;
        SetHeart(_maxHeart);
    }
    public static int GetHeart()
    {
        return PlayerPrefs.GetInt(HEART, 5);
    }

    public static void SetHeart(int value)
    {
        PlayerPrefs.SetInt(HEART, value);
        ObserverController.Instance.UpdateHeart();
    }

    public static void ReceiveHeart(int value, string placement = null)
    {
        int newHeart = Mathf.Min(GetHeart() + value, GetMaxHeart());
        SetHeart(newHeart);
        if(placement != null)
        {
            FirebaseManager.LogEarnItem(HEART, value, placement);
        }
    }

    public static void LostHeart(int value, string placement = null)
    {
        int currentHeart = GetHeart();
        if (currentHeart == GetMaxHeart())
        {
            SetLastTimeLoseHeart(DateTime.Now);
        }
        if(placement != null)
        {
            FirebaseManager.LogSpendItem(HEART, value, placement);
        }
        int newHeartValue = Math.Max(currentHeart - value, 0);
        SetHeart(newHeartValue);
    }

    public static DateTime GetLastTimeLoseHeart()
    {
        if (!PlayerPrefs.HasKey(LastTimeLoseHeart)) return DateTime.MinValue;
        string dateTimeString = PlayerPrefs.GetString(LastTimeLoseHeart);
        return DateTime.TryParse(dateTimeString, out DateTime savedDateTime) ? savedDateTime : DateTime.MinValue;
    }

    public static void SetLastTimeLoseHeart(DateTime value)
    {
        var dateTimeString = value.ToString("yyyy-MM-dd HH:mm:ss");
        PlayerPrefs.SetString(LastTimeLoseHeart, dateTimeString);
    }

    public static bool CheckInfinityHeart()
    {
        return GetTimeInfinityHeart() >= DateTime.Now;
    }

    public static DateTime GetTimeInfinityHeart() => GetDateTime(TimeInfinityHeart);

    public static void SetTimeInfinityHeart(TimeSpan value, string placement)
    {
        FirebaseManager.LogEarnItem("infinity_heart", (float)value.TotalSeconds, placement);
        if (CheckInfinityHeart())
        {
            var timeSpan = GetTimeInfinityHeart().Add(value);
            var dateTimeString = timeSpan.ToString("yyyy-MM-dd HH:mm:ss");
            PlayerPrefs.SetString(TimeInfinityHeart, dateTimeString);
        }
        else
        {
            var timeSpan = DateTime.Now.Add(value);
            var dateTimeString = timeSpan.ToString("yyyy-MM-dd HH:mm:ss");
            PlayerPrefs.SetString(TimeInfinityHeart, dateTimeString);
        }

        SetHeart(GetHeart() < GetMaxHeart() ? GetMaxHeart() : GetHeart());
    }

    #endregion

    #region TIME
    private static DateTime GetDateTime(string key)
        => PlayerPrefs.HasKey(key) && DateTime.TryParse(PlayerPrefs.GetString(key), out DateTime savedDateTime)
            ? savedDateTime
            : DateTime.MinValue;

    #endregion

    #region Coin

    private static string coin = "coin";

    public static int GetCoin()
    {
        if (!PlayerPrefs.HasKey(coin))
        {
            PlayerPrefs.SetInt(coin, 100);
            PlayerPrefs.Save();
            FirebaseManager.LogEarnResource(coin, 100, "free");
        }
        return PlayerPrefs.GetInt(coin, 100);
    }

    private static void UpdateCoin(int delta, string placement)
    {
        int newCoin = GetCoin() + delta;
        PlayerPrefs.SetInt(coin, newCoin);
        ObserverController.Instance.UpdateCoin();
        if (delta > 0)
        {
            FirebaseManager.LogEarnResource(coin, delta, placement);
        }
        else
        {
            FirebaseManager.LogSpendResource(coin, -delta, placement);
        }
    }

    public static void ReceiveCoin(int value, string placement)
    {
        UpdateCoin(value, placement);
    }

    public static bool SpendCoin(int value, string placement)
    {
        if (GetCoin() < value) return false;
     //   SoundManager.Instance.PlayOneShot(SoundType.Coin);
        UpdateCoin(-value, placement);
        return true;
    }


    #endregion

    #region Boosters
    private static string GetBoosterKey(BoosterType type) => $"BOOSTER_{type}";
    public static void SetBooster(BoosterType boosterType, int value)
    {
        PlayerPrefs.SetInt(GetBoosterKey(boosterType), value);
        ObserverController.Instance.OnBoosterChanged?.Invoke(boosterType, value);
    }

    public static int GetBooster(BoosterType type) => PlayerPrefs.GetInt(GetBoosterKey(type), 0);

    public static void ReceiveBooster(BoosterType type, int value, string placement)
    {
        int newValue = GetBooster(type) + value;
        FirebaseManager.LogEarnItem(type.ToString(), value, placement);
        PlayerPrefs.SetInt(GetBoosterKey(type), newValue);
        PlayerPrefs.Save();
        ObserverController.Instance.OnBoosterChanged?.Invoke(type, newValue);
    }

    public static bool SpendBooster(BoosterType type, int value, string placement)
    {
        if (IsFreeTutorialUseActive)
        {
            IsFreeTutorialUseActive = false;
            TriggerGameplayPlayed();
            FirebaseManager.LogSpendItem(type.ToString(), 1, "tutorial");
            ObserverController.Instance.OnBoosterChanged?.Invoke(type, GetBooster(type));
            return true;
        }

        if (GetBooster(type) < value) return false;

        TriggerGameplayPlayed();
        // GameManager.instance.HintUsed++;
        int newValue = GetBooster(type) - value;
        FirebaseManager.LogSpendItem(type.ToString(), value, placement);
        PlayerPrefs.SetInt(GetBoosterKey(type), newValue);

        ObserverController.Instance.OnBoosterChanged?.Invoke(type, newValue);
        return true;
    }
    #endregion

    #region Tutorial Boosters
    public static bool IsFreeTutorialUseActive { get; set; } = false;
    public const string TutorialKey = "Tutorial_Booster_Shown_";

    private static HashSet<BoosterType> shownBoosterTutorials = new System.Collections.Generic.HashSet<BoosterType>();

    public static bool IsBoosterTutorialShown(BoosterType type)
    {
        if (shownBoosterTutorials.Contains(type)) return true;
        bool shown = PlayerPrefs.GetInt($"{TutorialKey + type}", 0) == 1;
        if (shown) shownBoosterTutorials.Add(type);
        return shown;
    }

    public static void SetBoosterTutorialShown(BoosterType type)
    {
        PlayerPrefs.SetInt($"{TutorialKey + type}", 1);
        PlayerPrefs.Save();
        shownBoosterTutorials.Add(type);
    }
    #endregion

    #region Tutorial Hand
    public const string HandTutorialKey = "Tutorial_Hand_Completed";

    public static bool IsHandTutorialCompleted(int level = 1)
    {
        if (level == 1) return PlayerPrefs.GetInt(HandTutorialKey, 0) == 1;
        return PlayerPrefs.GetInt($"{HandTutorialKey}_{level}", 0) == 1;
    }

    public static void SetHandTutorialCompleted(int level = 1)
    {
        if (level == 1) PlayerPrefs.SetInt(HandTutorialKey, 1);
        PlayerPrefs.SetInt($"{HandTutorialKey}_{level}", 1);
        PlayerPrefs.Save();
    }
    #endregion

    #region StaterPack

    public const string StaterPackExpirationTime = "StarterPack_ExpirationTime";

    public static DateTime GetStarterPackExpirationTime()
    {
        string ticksStr = PlayerPrefs.GetString(StaterPackExpirationTime, "");
        if (!string.IsNullOrEmpty(ticksStr) && long.TryParse(ticksStr, out long ticks))
        {
            DateTime time = new DateTime(ticks);
            if (time > DateTime.UtcNow)
            {
                return time;
            }
        }
        return DateTime.MinValue;
    }

    public static void SetStarterPackExpirationTime(DateTime time)
    {
        PlayerPrefs.SetString(StaterPackExpirationTime, time.Ticks.ToString());
        PlayerPrefs.Save();
    }

    public static void ResetStarterPackExpirationTime()
    {
        DateTime newExpiration = DateTime.UtcNow.AddDays(3);
        SetStarterPackExpirationTime(newExpiration);
    }

    #endregion

    #region Pack
    private static string GetPackPurchasedKey(TypePackShop packType)
    {
        return "Purchase_Pack_" + packType;
    }

    public static void SetPackPurchased(TypePackShop packType)
    {
        if (IsNoAdsPack(packType))
        {
            PlayerPrefs.SetInt(GetPackPurchasedKey(TypePackShop.NOADS_PACK), 1);
            PlayerPrefs.SetInt(GetPackPurchasedKey(TypePackShop.NOADS_NORMAL), 1);
          //  SDKDGManager.Instance.SetNoAds(true);
           // SDKDGManager.Instance.SetNoAdsVip(true);
        }
        else
        {
            PlayerPrefs.SetInt(GetPackPurchasedKey(packType), 1);
        }
        PlayerPrefs.Save();
    }
    public static void NoAds()
    {
        PlayerPrefs.SetInt(GetPackPurchasedKey(TypePackShop.NOADS_PACK), 1);
        PlayerPrefs.SetInt(GetPackPurchasedKey(TypePackShop.NOADS_NORMAL), 1);
       // SDKDGManager.Instance.SetNoAds(true);
        //SDKDGManager.Instance.SetNoAdsVip(true);
    }

    public static bool IsPackPurchased(TypePackShop packType)
    {
        if (IsNoAdsPack(packType))
        {
            return PlayerPrefs.GetInt(GetPackPurchasedKey(TypePackShop.NOADS_PACK), 0) == 1 ||
                   PlayerPrefs.GetInt(GetPackPurchasedKey(TypePackShop.NOADS_NORMAL), 0) == 1;
        }

        return PlayerPrefs.GetInt(GetPackPurchasedKey(packType), 0) == 1;
    }

    private static bool IsNoAdsPack(TypePackShop packType)
    {
        return packType == TypePackShop.NOADS_NORMAL || packType == TypePackShop.NOADS_PACK;
    }
    #endregion

    #region OUT OF COIN
    private const string PURCHASED_IDS_KEY = "PURCHASED_IDS_KEY";
    private const string OFFER_START_TIME_KEY = "OFFER_START_TIME_KEY";
    private const int OFFER_DURATION_MINUTES = 5;

    public static List<int> GetPurchasedPackageIds()
    {
        if (!PlayerPrefs.HasKey(PURCHASED_IDS_KEY))
            return new List<int>();

        string raw = PlayerPrefs.GetString(PURCHASED_IDS_KEY);
        return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.TryParse(id, out int result) ? result : -1)
            .Where(id => id != -1)
            .ToList();
    }

    public static bool IsPackagePurchased(int packageId)
    {
        return GetPurchasedPackageIds().Contains(packageId);
    }

    public static void AddPurchasedPackage(int packageId)
    {
        var list = GetPurchasedPackageIds();
        if (!list.Contains(packageId))
        {
            list.Add(packageId);
            PlayerPrefs.SetString(PURCHASED_IDS_KEY, string.Join(",", list));
            PlayerPrefs.Save();
        }
    }
    public static double GetLoopingRemainingSeconds()
    {
        if (!PlayerPrefs.HasKey(OFFER_START_TIME_KEY))
        {
            PlayerPrefs.SetString(OFFER_START_TIME_KEY, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            PlayerPrefs.Save();
        }

        string startStr = PlayerPrefs.GetString(OFFER_START_TIME_KEY);
        if (DateTime.TryParse(startStr, out DateTime startTime))
        {
            double totalSecondsElapsed = (DateTime.Now - startTime).TotalSeconds;
            double cycleSeconds = OFFER_DURATION_MINUTES * 60;
            double remainder = totalSecondsElapsed % cycleSeconds;
            return cycleSeconds - remainder;
        }
        return OFFER_DURATION_MINUTES * 60;
    }

    public static void ResetPackCoin()
    {
        PlayerPrefs.DeleteKey(PURCHASED_IDS_KEY);
    }

    public static void DeletePackCoin()
    {
        PlayerPrefs.DeleteKey(PURCHASED_IDS_KEY);
        PlayerPrefs.DeleteKey(OFFER_START_TIME_KEY);
    }
    #endregion

    #region GAMEPLAY INTERACTION FLAG
    public static bool isPlayed = false;
    private const string PendingHeart = "PendingHeartDeduction";

    public static bool IsPendingHeart
    {
        get => PlayerPrefs.GetInt(PendingHeart, 0) == 1;
        set
        {
            if (value)
            {
                PlayerPrefs.SetInt(PendingHeart, 1);
            }
            else
            {
                PlayerPrefs.DeleteKey(PendingHeart);
            }
            PlayerPrefs.Save();
        }
    }

    public static void TriggerGameplayPlayed()
    {
        if (!isPlayed)
        {
            // GameManager.instance.startTime = Time.timeSinceLevelLoad;
            isPlayed = true;
            IsPendingHeart = true;
            // if (ObserverController.Instance != null)
            // {
            //     ObserverController.Instance.TriggerStartPlay();
            // }
        }
    }

    public static void ClearPendingHeart()
    {
        IsPendingHeart = false;
        isPlayed = false;
    }
    #endregion

    #region IN-GAME ADS POPUP
    private const string HasShownAdsIngameKey = "HasShownAdsIngame";

    public static bool HasShownAdsIngame
    {
        get => PlayerPrefs.GetInt(HasShownAdsIngameKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(HasShownAdsIngameKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
    #endregion
}
public enum TypePackShop
{
    STARTER_PACK,
    NOADS_PACK,
    NOADS_NORMAL,
    PACK_COIN1,
    PACK_COIN2,
    PACK_COIN3,
    PACK_COIN4,
    PACK_COIN5,
    PACK_COIN6,
    BOOSTER_PACK_ENDGAME,
    GREAT_BUNDLE,
    SUPER_BUNDLE,
    DELUXE_BUNDLE,
    EPIC_BUNDLE,
    ULTIMATE_BUNDLE,
    COIN_SHOWER_PACK_1,
    COIN_SHOWER_PACK_2, 
    COIN_SHOWER_PACK_3,
    FAIL_PACK,
}
public enum SceneType
{
    GamePlay,
    Lobby
}

#region STUBS FOR EXTERNAL DEPENDENCIES
public static class FirebaseManager
{
    public static void LogEarnItem(string item, float val, string placement) {}
    public static void LogSpendItem(string item, float val, string placement) {}
    public static void LogEarnResource(string item, float val, string placement) {}
    public static void LogSpendResource(string item, float val, string placement) {}
}

public class ObserverController
{
    private static ObserverController _instance;
    public static ObserverController Instance => _instance ??= new ObserverController();
    public System.Action<BoosterType, int> OnBoosterChanged;
    public void UpdateHeart() {}
    public void UpdateCoin() {}
}

public class GameController : MonoBehaviour
{
    public static GameController instance;
    public GameConfig GameConfig = new GameConfig();
}

public class GameConfig
{
    public int MaxLevel = 1000;
}
#endregion
