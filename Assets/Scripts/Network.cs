// Copyright 2022 bitHeads, Inc. All Rights Reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using BrainCloud.LitJson;

public class AuthenticationData
{
    Leaderboard m_MainLeaderboard = null;
    Leaderboard m_DailyLeaderboard = null;
    Leaderboard m_CountryLeaderboard = null;
    List<Statistic> m_StatisticsList = null;
    List<LevelData> m_LevelDataList = null;
    List<Achievement> m_AchievementsList = null;
    UserData m_UserProgress = null;

    public Leaderboard MainLeaderboard
    {
        get { return m_MainLeaderboard; }
        set { m_MainLeaderboard = value; }
    }

    public Leaderboard DailyLeaderboard
    {
        get { return m_DailyLeaderboard; }
        set { m_DailyLeaderboard = value; }
    }

    public Leaderboard CountryLeaderboard
    {
        get { return m_CountryLeaderboard; }
        set { m_CountryLeaderboard = value; }
    }

    public List<Statistic> StatisticsList
    {
        get { return m_StatisticsList; }
        set { m_StatisticsList = value; }
    }

    public List<LevelData> LevelDataList
    {
        get { return m_LevelDataList; }
        set { m_LevelDataList = value; }
    }

    public List<Achievement> AchievementsList
    {
        get { return m_AchievementsList; }
        set { m_AchievementsList = value; }
    }

    public UserData UserProgress
    {
        get { return m_UserProgress; }
        set { m_UserProgress = value; }
    }
}


public class Network : MonoBehaviour
{
    public delegate void AuthenticationRequestCompleted(ref AuthenticationData authenticationData);
    public delegate void AuthenticationRequestFailed(string errorMessage);
    public delegate void BrainCloudLogOutCompleted();
    public delegate void BrainCloudLogOutFailed();
    public delegate void UpdateUsernameRequestCompleted();
    public delegate void UpdateUsernameRequestFailed();
    public delegate void LeaderboardRequestCompleted(Leaderboard leaderboard);
    public delegate void LeaderboardRequestFailed();
    public delegate void PostScoreRequestCompleted();
    public delegate void PostScoreRequestFailed();
    public delegate void RequestGlobalEntityLevelDataCompleted(ref List<LevelData> levels);
    public delegate void RequestGlobalEntityLevelDataFailed();
    public delegate void UserStatisticsRequestCompleted(ref List<Statistic> statistics);
    public delegate void UserStatisticsRequestFailed();
    public delegate void IncrementUserStatisticsCompleted(ref List<Statistic> statistics);
    public delegate void IncrementUserStatisticsFailed();
    public delegate void ReadAchievementRequestCompleted(ref List<Achievement> achievements);
    public delegate void ReadAchievementRequestFailed();
    public delegate void AchievementAwardedCompleted();
    public delegate void AchievementAwardedFailed();
    public delegate void RequestUserEntityDataCompleted(UserData userData);
    public delegate void RequestUserEntityDataFailed();
    public delegate void CreateUserEntityDataCompleted();
    public delegate void CreateUserEntityDataFailed();
    public delegate void UpdateUserEntityDataCompleted();
    public delegate void UpdateUserEntityDataFailed();
    public delegate void GetIdentitiesRequestCompleted();
    public delegate void GetIdentitiesRequestFailed();
    public delegate void AttachEmailIdentityCompleted();
    public delegate void AttachEmailIdentityFailed();

    public static Network sharedInstance;

    private BrainCloudWrapper m_BrainCloud;
    private string m_Username;
    List<string> m_IdentityTypesList = new List<string>();
    private TwitchHelper m_TwitchHelper = null;
    private AuthenticationRequestCompleted m_AuthenticationRequestCompleted = null;
    private AuthenticationRequestFailed m_AuthenticationRequestFailed = null;


    private void Awake()
    {
        sharedInstance = this;

        DontDestroyOnLoad(gameObject);

        // Create and initialize the BrainCloud wrapper
        m_BrainCloud = gameObject.AddComponent<BrainCloudWrapper>();
        m_BrainCloud.Init();

        // Log the BrainCloud client version
        Debug.Log("BrainCloud client version: " + m_BrainCloud.Client.BrainCloudClientVersion);
    }

    void Update()
    {
        // Make sure you invoke this method in Update, or else you won't get any callbacks
        m_BrainCloud.RunCallbacks();
    }

    public List<string> IdentityTypesList
    {
        get { return m_IdentityTypesList; }
    }

    public bool HasAuthenticatedPreviously()
    {
        return m_BrainCloud.GetStoredProfileId() != "" && m_BrainCloud.GetStoredAnonymousId() != "";
    }

    public bool IsAuthenticated()
    {
        return m_BrainCloud.Client.Authenticated;
    }

    public bool IsUsernameSaved()
    {
        return m_Username != "";
    }

    public string GetUsername()
    {
        return m_Username;
    }

    public void ResetStoredProfileId()
    {
        m_BrainCloud.ResetStoredProfileId();
    }

    public void LogOut(BrainCloudLogOutCompleted brainCloudLogOutCompleted = null, BrainCloudLogOutFailed brainCloudLogOutFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("LogOut success: " + responseData);

                m_Username = "";

                // The user logged out, clear the persisted data related to their account
                m_BrainCloud.ResetStoredAnonymousId();
                m_BrainCloud.ResetStoredProfileId();

                if (brainCloudLogOutCompleted != null)
                    brainCloudLogOutCompleted();
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("BrainCloud Logout failed: " + statusMessage);

                if (brainCloudLogOutFailed != null)
                    brainCloudLogOutFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.PlayerStateService.Logout(successCallback, failureCallback);
        }
        else
        {
            Debug.Log("BrainCloud Logout failed: user is not authenticated");

            if (brainCloudLogOutFailed != null)
                brainCloudLogOutFailed();
        }
    }

    public void Reconnect(AuthenticationRequestCompleted authenticationRequestCompleted = null, AuthenticationRequestFailed authenticationRequestFailed = null)
    {
        // Success callback lambda
        BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
        {
            Debug.Log("Reconnect success: " + responseData);
            HandleAuthenticationSuccess(responseData, cbObject, authenticationRequestCompleted);
        };

        // Failure callback lambda
        BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
        {
            if (code == BrainCloud.ReasonCodes.GAME_VERSION_NOT_SUPPORTED)
            {
                HandleAppVersionError(error, authenticationRequestFailed);
            }
            else
            {
                string errorMessage = "Reconnect authentication failed. " + ExtractStatusMessage(error);
                HandleAuthenticationError(errorMessage, authenticationRequestFailed);
            }
        };

        // Make the BrainCloud request
        m_BrainCloud.Reconnect(successCallback, failureCallback);
    }

    public void RequestAnonymousAuthentication(AuthenticationRequestCompleted authenticationRequestCompleted = null, AuthenticationRequestFailed authenticationRequestFailed = null)
    {
        // Success callback lambda
        BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
        {
            Debug.Log("RequestAnonymousAuthentication success: " + responseData);
            HandleAuthenticationSuccess(responseData, cbObject, authenticationRequestCompleted);
        };

        // Failure callback lambda
        BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
        {
            if (code == BrainCloud.ReasonCodes.GAME_VERSION_NOT_SUPPORTED)
            {
                HandleAppVersionError(error, authenticationRequestFailed);
            }
            else
            {
                string errorMessage = "Anonymous authentication failed. " + ExtractStatusMessage(error);
                HandleAuthenticationError(errorMessage, authenticationRequestFailed);
            }
        };

        // Make the BrainCloud request
        m_BrainCloud.AuthenticateAnonymous(successCallback, failureCallback);
    }

    public void RequestAuthenticationEmail(string email, string password, AuthenticationRequestCompleted authenticationRequestCompleted = null, AuthenticationRequestFailed authenticationRequestFailed = null)
    {
        // Success callback lambda
        BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
        {
            Debug.Log("RequestAuthenticationEmail success: " + responseData);
            HandleAuthenticationSuccess(responseData, cbObject, authenticationRequestCompleted);
        };

        // Failure callback lambda
        BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
        {
            if (code == BrainCloud.ReasonCodes.GAME_VERSION_NOT_SUPPORTED)
            {
                HandleAppVersionError(error, authenticationRequestFailed);
            }
            else
            {
                string errorMessage = "Email authentication failed. " + ExtractStatusMessage(error);
                HandleAuthenticationError(errorMessage, authenticationRequestFailed);
            }
        };

        // Make the BrainCloud request
        m_BrainCloud.AuthenticateEmailPassword(email, password, true, successCallback, failureCallback);
    }

    public void RequestAuthenticationUniversal(string userID, string password, AuthenticationRequestCompleted authenticationRequestCompleted = null, AuthenticationRequestFailed authenticationRequestFailed = null)
    {
        // Success callback lambda
        BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
        {
            Debug.Log("RequestAuthenticationUniversal success: " + responseData);
            HandleAuthenticationSuccess(responseData, cbObject, authenticationRequestCompleted);
        };

        // Failure callback lambda
        BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
        {
            if (code == BrainCloud.ReasonCodes.GAME_VERSION_NOT_SUPPORTED)
            {
                HandleAppVersionError(error, authenticationRequestFailed);
            }
            else
            {
                string errorMessage = "Universal authentication failed. " + ExtractStatusMessage(error);
                HandleAuthenticationError(errorMessage, authenticationRequestFailed);
            }
        };

        // Make the BrainCloud request
        m_BrainCloud.AuthenticateUniversal(userID, password, true, successCallback, failureCallback);
    }

    public void RequestTwitchAuthentication(AuthenticationRequestCompleted authenticationRequestCompleted = null, AuthenticationRequestFailed authenticationRequestFailed = null)
    {
        // Create the TwitchHelper object, if its not already created
        if (m_TwitchHelper == null)
            m_TwitchHelper = new TwitchHelper(Constants.kTwitchClientId, Constants.kTwitchClientSecret, Constants.kTwitchRedirectUrl);

        // Initialize the authentication delegates
        m_AuthenticationRequestCompleted = authenticationRequestCompleted;
        m_AuthenticationRequestFailed = authenticationRequestFailed;

        // List of scopes we want access to 
        string[] scopes = new[]
        {
            "user:read:email"
        };

        // Generate an auth state "state" parameter. It's gonna be echoed back to verify the redirect call is from Twitch
        string authState = ((Int64)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString();

        // Query parameters for the Twitch auth URL
        string parameters = "client_id=" + Constants.kTwitchClientId + "&" +
            "force_verify=true&" +
            "redirect_uri=" + UnityWebRequest.EscapeURL(Constants.kTwitchRedirectUrl) + "&" +
            "state=" + authState + "&" +
            "response_type=code&" +
            "scope=" + String.Join("+", scopes);

        // Start our local webserver to receive the redirect back after Twitch authenticated
        m_TwitchHelper.StartLocalCallbackServer(authState, OnTwitchAuthenticationGranted, OnTwitchAuthenticationDenied);

        // Open the users browser and send them to the Twitch auth URL
        Application.OpenURL(Constants.kTwitchAuthUrl + "?" + parameters);
    }

    public void RequestUpdateUsername(string username, UpdateUsernameRequestCompleted updateUsernameRequestCompleted = null, UpdateUsernameRequestFailed updateUsernameRequestFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("RequestUpdateUsername success: " + responseData);

                JsonData jsonData = JsonMapper.ToObject(responseData);
                m_Username = jsonData["data"]["playerName"].ToString();

                if (updateUsernameRequestCompleted != null)
                    updateUsernameRequestCompleted();
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("RequestUpdateUsername failed: " + statusMessage);

                if (updateUsernameRequestFailed != null)
                    updateUsernameRequestFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.PlayerStateService.UpdateName(username, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("RequestUpdateUsername failed: user is not authenticated");

            if (updateUsernameRequestFailed != null)
                updateUsernameRequestFailed();
        }
    }

    public void RequestLeaderboard(string leaderboardId, LeaderboardRequestCompleted leaderboardRequestCompleted = null, LeaderboardRequestFailed leaderboardRequestFailed = null)
    {
        RequestLeaderboard(leaderboardId, Constants.kBrainCloudDefaultMinHighScoreIndex, Constants.kBrainCloudDefaultMaxHighScoreIndex, leaderboardRequestCompleted, leaderboardRequestFailed);
    }

    public void RequestLeaderboard(string leaderboardId, int startIndex, int endIndex, LeaderboardRequestCompleted leaderboardRequestCompleted = null, LeaderboardRequestFailed leaderboardRequestFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("RequestLeaderboard success: " + responseData);

                // Read the json and update our values
                JsonData jsonData = JsonMapper.ToObject(responseData);
                JsonData leaderboardData = jsonData["data"];
                Leaderboard leaderboard = ParseLeaderboard(ref leaderboardData);

                if (leaderboardRequestCompleted != null)
                    leaderboardRequestCompleted(leaderboard);
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("RequestLeaderboard failed: " + statusMessage);

                if (leaderboardRequestFailed != null)
                    leaderboardRequestFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.LeaderboardService.GetGlobalLeaderboardPage(leaderboardId, BrainCloud.BrainCloudSocialLeaderboard.SortOrder.HIGH_TO_LOW, startIndex, endIndex, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("RequestLeaderboard failed: user is not authenticated");

            if (leaderboardRequestFailed != null)
                leaderboardRequestFailed();
        }
    }

    public void PostScoreToLeaderboard(string leaderboardID, float time, PostScoreRequestCompleted postScoreRequestCompleted = null, PostScoreRequestFailed postScoreRequestFailed = null)
    {
        PostScoreToLeaderboard(leaderboardID, time, GetUsername(), postScoreRequestCompleted, postScoreRequestFailed);
    }

    public void PostScoreToLeaderboard(string leaderboardID, float time, string nickname, PostScoreRequestCompleted postScoreRequestCompleted = null, PostScoreRequestFailed postScoreRequestFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("PostScoreToLeaderboard success: " + responseData);

                if (postScoreRequestCompleted != null)
                    postScoreRequestCompleted();
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("PostScoreToLeaderboard failed: " + statusMessage);

                if (postScoreRequestFailed != null)
                    postScoreRequestFailed();
            };

            // Make the BrainCloud request
            long score = (long)(time * 1000.0f);   // Convert the time from seconds to milleseconds
            string jsonOtherData = "{\"nickname\":\"" + nickname + "\"}";
            m_BrainCloud.LeaderboardService.PostScoreToLeaderboard(leaderboardID, score, jsonOtherData, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("PostScoreToLeaderboard failed: user is not authenticated");

            if (postScoreRequestFailed != null)
                postScoreRequestFailed();
        }
    }

    public void PostScoreToLeaderboards(float time, PostScoreRequestCompleted postScoreRequestCompleted = null, PostScoreRequestFailed postScoreRequestFailed = null)
    {
        PostScoreToLeaderboards(time, GetUsername(), postScoreRequestCompleted, postScoreRequestFailed);
    }

    public void PostScoreToLeaderboards(float time, string nickname, PostScoreRequestCompleted postScoreRequestCompleted = null, PostScoreRequestFailed postScoreRequestFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("PostScoreToLeaderboards success: " + responseData);

                if (postScoreRequestCompleted != null)
                    postScoreRequestCompleted();
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("PostScoreToLeaderboards failed: " + statusMessage);

                if (postScoreRequestFailed != null)
                    postScoreRequestFailed();
            };

            long score = (long)(time * 1000.0f);   // Convert the time from seconds to milleseconds
            string jsonScriptData = "{\"leaderboards\":[\"Main\", \"Daily\"],\"score\":" + score + ",\"extras\":{\"nickname\":\"" + nickname + "\"}}";


            m_BrainCloud.ScriptService.RunScript("PostToLeaderboards", jsonScriptData, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("PostScoreToLeaderboards failed: user is not authenticated");

            if (postScoreRequestFailed != null)
                postScoreRequestFailed();
        }
    }

    public void RequestGlobalEntityLevelData(RequestGlobalEntityLevelDataCompleted requestGlobalEntityLevelDataCompleted = null, RequestGlobalEntityLevelDataFailed requestGlobalEntityLevelDataFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("RequestGlobalEntityLevelData success: " + responseData);

                // Read the json and update our values
                JsonData jsonData = JsonMapper.ToObject(responseData);
                JsonData entityData = jsonData["data"];
                List<LevelData> levelData = ParseLevelData(ref entityData);

                if (requestGlobalEntityLevelDataCompleted != null)
                    requestGlobalEntityLevelDataCompleted(ref levelData);
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("RequestUserEntityData failed: " + statusMessage);

                if (requestGlobalEntityLevelDataFailed != null)
                    requestGlobalEntityLevelDataFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.GlobalEntityService.GetListByIndexedId(Constants.kBrainCloudGlobalEntityIndexedID, 5, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("RequestUserEntityData failed: user is not authenticated");

            if (requestGlobalEntityLevelDataFailed != null)
                requestGlobalEntityLevelDataFailed();
        }
    }

    public void RequestUserStatistics(UserStatisticsRequestCompleted userStatisticsRequestCompleted = null, UserStatisticsRequestFailed userStatisticsRequestFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("RequestUserStatistics success: " + responseData);

                // Read the json and update our values
                JsonData jsonData = JsonMapper.ToObject(responseData);
                JsonData statistics = jsonData["data"]["statistics"];
                List<Statistic> statisticsList = ParseStatistics(ref statistics);

                if (userStatisticsRequestCompleted != null)
                    userStatisticsRequestCompleted(ref statisticsList);
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("RequestUserStatistics failed: " + statusMessage);

                if (userStatisticsRequestFailed != null)
                    userStatisticsRequestFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.PlayerStatisticsService.ReadAllUserStats(successCallback, failureCallback);
        }
        else
        {
            Debug.Log("RequestUserStatistics failed: user is not authenticated");

            if (userStatisticsRequestFailed != null)
                userStatisticsRequestFailed();
        }
    }

    public void IncrementUserStatistics(Dictionary<string, object> data, IncrementUserStatisticsCompleted incrementUserStatisticsCompleted = null, IncrementUserStatisticsFailed incrementUserStatisticsFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("IncrementUserStatistics success: " + responseData);

                // Read the json and update our values
                JsonData jsonData = JsonMapper.ToObject(responseData);
                JsonData statistics = jsonData["data"]["statistics"];

                List<Statistic> statisticsList = new List<Statistic>();

                long value = 0;
                string description;
                foreach (string key in statistics.Keys)
                {
                    value = long.Parse(statistics[key].ToString());
                    description = Constants.kBrainCloudStatDescriptions[key];
                    statisticsList.Add(new Statistic(key, description, value));
                }

                if (incrementUserStatisticsCompleted != null)
                    incrementUserStatisticsCompleted(ref statisticsList);
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("IncrementUserStatistics failed: " + statusMessage);

                if (incrementUserStatisticsFailed != null)
                    incrementUserStatisticsFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.PlayerStatisticsService.IncrementUserStats(data, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("IncrementUserStatistics failed: user is not authenticated");

            if (incrementUserStatisticsFailed != null)
                incrementUserStatisticsFailed();
        }
    }

    public void RequestReadAchievements(ReadAchievementRequestCompleted readAchievementRequestCompleted = null, ReadAchievementRequestFailed readAchievementRequestFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("RequestReadAchievements success: " + responseData);

                // Read the json and update our values
                JsonData jsonData = JsonMapper.ToObject(responseData);
                JsonData achievements = jsonData["data"]["achievements"];
                List<Achievement> achievementsList = ParseAchievements(ref achievements);

                if (readAchievementRequestCompleted != null)
                    readAchievementRequestCompleted(ref achievementsList);
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("RequestReadAchievements failed: " + statusMessage);

                if (readAchievementRequestFailed != null)
                    readAchievementRequestFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.GamificationService.ReadAchievements(true, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("RequestAchievements failed: user is not authenticated");

            if (readAchievementRequestFailed != null)
                readAchievementRequestFailed();
        }
    }

    public void AwardAchievementRequest(Achievement achievement, AchievementAwardedCompleted achievementAwardedCompleted = null, AchievementAwardedFailed achievementAwardedFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("AwardAchievementRequest success: " + responseData);

                if (achievementAwardedCompleted != null)
                    achievementAwardedCompleted();
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("AwardAchievementRequest failed: " + statusMessage);

                if (achievementAwardedFailed != null)
                    achievementAwardedFailed();
            };

            // Make the BrainCloud request
            string[] achievements = { achievement.ID };
            m_BrainCloud.GamificationService.AwardAchievements(achievements, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("AwardAchievementRequest failed: user is not authenticated");

            if (achievementAwardedFailed != null)
                achievementAwardedFailed();
        }
    }

    public void RequestUserEntityData(RequestUserEntityDataCompleted requestUserEntityDataCompleted = null, RequestUserEntityDataFailed requestUserEntityDataFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("RequestUserEntityData success: " + responseData);

                JsonData jsonData = JsonMapper.ToObject(responseData);
                JsonData entities = jsonData["data"]["entities"];

                UserData userData = null;

                if (entities.IsArray && entities.Count > 0)
                {
                    JsonData entityData = entities[0];
                    userData = ParseUserProgress(ref entityData);
                }

                if (requestUserEntityDataCompleted != null)
                    requestUserEntityDataCompleted(userData);
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("RequestUserEntityData failed: " + statusMessage);

                if (requestUserEntityDataFailed != null)
                    requestUserEntityDataFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.EntityService.GetEntitiesByType(Constants.kBrainCloudUserProgressUserEntityType, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("RequestUserEntityData failed: user is not authenticated");

            if (requestUserEntityDataFailed != null)
                requestUserEntityDataFailed();
        }
    }

    public void CreateUserEntityData(CreateUserEntityDataCompleted createUserEntityDataCompleted = null, CreateUserEntityDataFailed createUserEntityDataFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("CreateUserEntityData success: " + responseData);

                if (createUserEntityDataCompleted != null)
                    createUserEntityDataCompleted();
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("CreateUserEntityData failed: " + statusMessage);

                if (createUserEntityDataFailed != null)
                    createUserEntityDataFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.EntityService.CreateEntity(Constants.kBrainCloudUserProgressUserEntityType,
                                                    Constants.kBrainCloudUserProgressUserEntityDefaultData,
                                                    Constants.kBrainCloudUserProgressUserEntityDefaultAcl,
                                                    successCallback, failureCallback);
        }
        else
        {
            Debug.Log("CreateUserEntityData failed: user is not authenticated");

            if (createUserEntityDataFailed != null)
                createUserEntityDataFailed();
        }
    }

    public void UpdateUserEntityData(string entityID, string entityType, string jsonData, UpdateUserEntityDataCompleted updateUserEntityDataCompleted = null, UpdateUserEntityDataFailed updateUserEntityDataFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("UpdateUserEntityData success: " + responseData);

                if (updateUserEntityDataCompleted != null)
                    updateUserEntityDataCompleted();
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("UpdateUserEntityData failed: " + statusMessage);

                if (updateUserEntityDataFailed != null)
                    updateUserEntityDataFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.EntityService.UpdateEntity(entityID, entityType, jsonData, Constants.kBrainCloudUserProgressUserEntityDefaultAcl, -1, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("UpdateUserEntityData failed: user is not authenticated");

            if (updateUserEntityDataFailed != null)
                updateUserEntityDataFailed();
        }
    }

    public void RequestGetIdentities(GetIdentitiesRequestCompleted getIdentitiesRequestCompleted = null, GetIdentitiesRequestFailed getIdentitiesRequestFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("RequestGetIdentities success: " + responseData);

                JsonData jsonData = JsonMapper.ToObject(responseData);
                JsonData identities = jsonData["data"]["identities"];

                // Clear the Identity types list before adding new identities to it
                m_IdentityTypesList.Clear();

                // Add the non-anonymous identities to the identity types list
                foreach (string key in identities.Keys)
                    m_IdentityTypesList.Add(key);

                if (getIdentitiesRequestCompleted != null)
                    getIdentitiesRequestCompleted();
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("RequestGetIdentities failed: " + statusMessage);

                if (getIdentitiesRequestFailed != null)
                    getIdentitiesRequestFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.IdentityService.GetIdentities(successCallback, failureCallback);
        }
        else
        {
            Debug.Log("RequestGetIdentities failed: user is not authenticated");

            if (getIdentitiesRequestFailed != null)
                getIdentitiesRequestFailed();
        }
    }

    public void AttachEmailIdentity(string email, string password, AttachEmailIdentityCompleted attachEmailIdentityCompleted = null, AttachEmailIdentityFailed attachEmailIdentityFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("AttachEmailIdentity success: " + responseData);

                // Ensure the Identity types list doesn't contain an email identity, if it doesn't add it
                if (!m_IdentityTypesList.Contains("Email"))
                    m_IdentityTypesList.Add("Email");

                if (attachEmailIdentityCompleted != null)
                    attachEmailIdentityCompleted();
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("AttachEmailIdentity failed: " + statusMessage);

                if (attachEmailIdentityFailed != null)
                    attachEmailIdentityFailed();
            };

            // Make the BrainCloud request
            m_BrainCloud.IdentityService.AttachEmailIdentity(email, password, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("AttachEmailIdentity failed: user is not authenticated");

            if (attachEmailIdentityFailed != null)
                attachEmailIdentityFailed();
        }
    }

    public void RequestCountryLeaderboard(LeaderboardRequestCompleted leaderboardRequestCompleted = null, LeaderboardRequestFailed leaderboardRequestFailed = null)
    {
        if (IsAuthenticated())
        {
            // Success callback lambda
            BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
            {
                Debug.Log("RequestCountryLeaderboard success: " + responseData);

                // Read the json and update our values
                JsonData jsonData = JsonMapper.ToObject(responseData);
                JsonData leaderboardData = jsonData["data"]["results"]["items"];

                List<LeaderboardEntry> leaderboardEntriesList = new List<LeaderboardEntry>();
                int rank = 0;
                string nickname;
                long ms = 0;
                float time = 0.0f;

                if (leaderboardData.IsArray)
                {
                    for (int i = 0; i < leaderboardData.Count; i++)
                    {
                        rank = i + 1;
                        nickname = leaderboardData[i]["data"]["countryCode"].ToString();
                        ms = long.Parse(leaderboardData[i]["data"]["score"].ToString());
                        time = (float)ms / 1000.0f;

                        leaderboardEntriesList.Add(new LeaderboardEntry(nickname, rank, time));
                    }
                }

                Leaderboard leaderboard = new Leaderboard(Constants.kBrainCloudCountryLeaderboardID, leaderboardEntriesList);

                if (leaderboardRequestCompleted != null)
                    leaderboardRequestCompleted(leaderboard);
            };

            // Failure callback lambda
            BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
            {
                Debug.Log("RequestCountryLeaderboard failed: " + ExtractStatusMessage(error));

                if (leaderboardRequestFailed != null)
                    leaderboardRequestFailed();
            };

            // Setup the json context data
            string jsonContext = "{\"pagination\": {\"rowsPerPage\": 10,\"pageNumber\": 1},\"searchCriteria\": {},\"sortCriteria\": {\"data.score\": -1}}"; ;

            // Make the BrainCloud request
            m_BrainCloud.CustomEntityService.GetEntityPage(Constants.kBrainCloudCountryLeaderboardEntityType, jsonContext, successCallback, failureCallback);
        }
        else
        {
            Debug.Log("RquestCountryLeaderboard failed: user is not authenticated");

            if (leaderboardRequestFailed != null)
                leaderboardRequestFailed();
        }
    }

    private void HandleAuthenticationSuccess(string responseData, object cbObject, AuthenticationRequestCompleted authenticationRequestCompleted)
    {
        AuthenticationData authenticationData = new AuthenticationData();

        // Read the player name from the response data
        JsonData jsonData = JsonMapper.ToObject(responseData);
        JsonData root = jsonData["data"];
        m_Username = root["playerName"].ToString();

        // Parse the Statistics data
        JsonData statisticsData = root["statistics"];
        authenticationData.StatisticsList = ParseStatistics(ref statisticsData);

        // Parse the Leaderboard data
        if (root.Keys.Contains("leaderboards"))
        {
            JsonData leaderboardsData = root["leaderboards"];

            // Parse the main leaderboard
            JsonData mainLeaderboardData = leaderboardsData["main"];
            authenticationData.MainLeaderboard = ParseLeaderboard(ref mainLeaderboardData);

            // Parse the daily leaderboard
            JsonData dailyLeaderboardData = leaderboardsData["daily"];
            authenticationData.DailyLeaderboard = ParseLeaderboard(ref dailyLeaderboardData);

            // Parse the country leaderboard
            JsonData countryLeaderboardData = leaderboardsData["country"];
            authenticationData.CountryLeaderboard = ParseLeaderboard(ref countryLeaderboardData);
        }

        // Parse the Global Entity level data
        if (root.Keys.Contains("levelData"))
        {
            JsonData levelEntityData = root["levelData"];
            authenticationData.LevelDataList = ParseLevelData(ref levelEntityData);
        }

        // Parse the achievements data
        if (root.Keys.Contains("achievements"))
        {
            JsonData achievementData = root["achievements"];
            authenticationData.AchievementsList = ParseAchievements(ref achievementData);
        }

        // Parse the user progress entity
        if (root.Keys.Contains("userProgress"))
        {
            JsonData userProgressData = root["userProgress"];
            authenticationData.UserProgress = ParseUserProgress(ref userProgressData);
        }

        // Parse the identities data
        if (root.Keys.Contains("identities"))
        {
            JsonData identitiesData = root["identities"];
            m_IdentityTypesList.Clear();

            foreach (string key in identitiesData.Keys)
                m_IdentityTypesList.Add(key);
        }

        if (authenticationRequestCompleted != null)
            authenticationRequestCompleted(ref authenticationData);
    }

    private List<Statistic> ParseStatistics(ref JsonData statisticsData)
    {
        List<Statistic> statisticsList = new List<Statistic>();
        long value = 0;
        string description;

        foreach (string key in statisticsData.Keys)
        {
            value = long.Parse(statisticsData[key].ToString());
            description = Constants.kBrainCloudStatDescriptions[key];
            statisticsList.Add(new Statistic(key, description, value));
        }

        return statisticsList;
    }

    private Leaderboard ParseLeaderboard(ref JsonData leaderboardData)
    {
        if (leaderboardData != null)
        {
            JsonData leaderboardArray = leaderboardData["leaderboard"];
            string leaderboardId = leaderboardData["leaderboardId"].ToString();

            List<LeaderboardEntry> leaderboardEntriesList = new List<LeaderboardEntry>();
            int rank = 0;
            string nickname;
            long ms = 0;
            float time = 0.0f;

            if (leaderboardArray.IsArray)
            {
                for (int i = 0; i < leaderboardArray.Count; i++)
                {
                    rank = int.Parse(leaderboardArray[i]["rank"].ToString());
                    nickname = leaderboardArray[i]["data"]["nickname"].ToString();
                    ms = long.Parse(leaderboardArray[i]["score"].ToString());
                    time = (float)ms / 1000.0f;

                    leaderboardEntriesList.Add(new LeaderboardEntry(nickname, rank, time));
                }
            }

            Leaderboard leaderboard = new Leaderboard(leaderboardId, leaderboardEntriesList);
            return leaderboard;
        }

        return null;
    }

    private List<LevelData> ParseLevelData(ref JsonData levelEntityData)
    {
        JsonData entityList = levelEntityData["entityList"];

        List<LevelData> levelData = new List<LevelData>();
        string entityType;
        string entityID;
        int index = 0;

        if (entityList.IsArray)
        {
            for (int i = 0; i < entityList.Count; i++)
            {
                JsonData level = entityList[i]["data"]["level"];
                entityType = entityList[i]["entityType"].ToString();
                entityID = entityList[i]["entityId"].ToString();
                index = int.Parse(entityList[i]["data"]["levelIndex"].ToString());
                levelData.Add(new LevelData(entityType, entityID, index, ref level));
            }
        }

        return levelData;
    }

    private List<Achievement> ParseAchievements(ref JsonData achievementsData)
    {
        List<Achievement> achievementsList = new List<Achievement>();
        string id;
        string title;
        string description;
        string status;

        if (achievementsData.IsArray)
        {
            for (int i = 0; i < achievementsData.Count; i++)
            {
                id = achievementsData[i]["id"].ToString();
                title = achievementsData[i]["title"].ToString();
                description = achievementsData[i]["description"].ToString();
                status = achievementsData[i]["status"].ToString();

                achievementsList.Add(new Achievement(id, title, description, status));
            }
        }

        return achievementsList;
    }

    private UserData ParseUserProgress(ref JsonData userProgressData)
    {
        // Parse the user progress entity
        string entityID = userProgressData["entityId"].ToString();
        string entityType = userProgressData["entityType"].ToString();
        UserData userData = new UserData(entityID, entityType);
        userData.LevelOneCompleted = bool.Parse(userProgressData["data"]["levelOneCompleted"].ToString());
        userData.LevelTwoCompleted = bool.Parse(userProgressData["data"]["levelTwoCompleted"].ToString());
        userData.LevelThreeCompleted = bool.Parse(userProgressData["data"]["levelThreeCompleted"].ToString());
        userData.LevelBossCompleted = bool.Parse(userProgressData["data"]["levelBossCompleted"].ToString());

        return userData;
    }

    private void HandleAuthenticationError(string errorMessage, AuthenticationRequestFailed authenticationRequestFailed = null)
    {
        Debug.Log(errorMessage);

        if (authenticationRequestFailed != null)
            authenticationRequestFailed(errorMessage);
    }

    private void HandleAppVersionError(string error, AuthenticationRequestFailed authenticationRequestFailed = null)
    {
        JsonData jsonData = JsonMapper.ToObject(error);
        string upgradeAppIdMessage = jsonData["upgradeAppId"].ToString();
        HandleAuthenticationError(upgradeAppIdMessage, authenticationRequestFailed);
    }

    private string ExtractStatusMessage(string error)
    {
        JsonData jsonData = JsonMapper.ToObject(error);
        string statusMessage = jsonData["status_message"].ToString();
        return statusMessage;
    }

    private void OnTwitchAuthenticationGranted(string accessToken, string userEmail, string username)
    {
        // Success callback lambda
        BrainCloud.SuccessCallback successCallback = (responseData, cbObject) =>
        {
            Debug.Log("AuthenticateExternal Twitch success: " + responseData);

            HandleAuthenticationSuccess(responseData, cbObject, m_AuthenticationRequestCompleted);

            if (username != m_Username)
                RequestUpdateUsername(username);
        };

        // Failure callback lambda
        BrainCloud.FailureCallback failureCallback = (statusMessage, code, error, cbObject) =>
        {
            if (code == BrainCloud.ReasonCodes.GAME_VERSION_NOT_SUPPORTED)
            {
                HandleAppVersionError(error, m_AuthenticationRequestFailed);
            }
            else
            {
                string errorMessage = "External authentication failed. " + ExtractStatusMessage(error);
                HandleAuthenticationError(errorMessage, m_AuthenticationRequestFailed);
            }
        };

        // Make the BrainCloud request
        m_BrainCloud.AuthenticateExternal(userEmail, accessToken, Constants.kBrainCloudExternalAuthTwitch, true, successCallback, failureCallback);
    }

    private void OnTwitchAuthenticationDenied()
    {
        string errorMessage = "Unable to authenticate with brainCloud. User denied Twitch OAuth 2.0 access";
        HandleAuthenticationError(errorMessage, m_AuthenticationRequestFailed);
    }
}
