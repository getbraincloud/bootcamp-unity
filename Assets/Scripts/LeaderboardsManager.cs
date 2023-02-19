// Copyright 2022 bitHeads, Inc. All Rights Reserved.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeaderboardsManager : MonoBehaviour
{
    public static LeaderboardsManager sharedInstance;

    private List<Leaderboard> m_Leaderboards;
    private float m_UserTime;

    private void Awake()
    {
        sharedInstance = this;
        m_Leaderboards = new List<Leaderboard>();
    }

    public void AddLeaderboard(Leaderboard leaderboard)
    {
        if (m_UserTime > 0.0f)
        {
            for (int i = 0; i < leaderboard.GetCount(); i++)
            {
                if (leaderboard.GetLeaderboardEntryAtIndex(i).Time == m_UserTime)
                {
                    leaderboard.GetLeaderboardEntryAtIndex(i).IsUserScore = true;
                    break;
                }
            }
        }

        // Remove all existing leaderboards with the same name
        m_Leaderboards.RemoveAll(p => p.Name == leaderboard.Name);

        // Add the Leaderboard object to the list
        m_Leaderboards.Add(leaderboard);
    }

    public Leaderboard GetLeaderboardByName(string name)
    {
        for (int i = 0; i < m_Leaderboards.Count; i++)
        {
            if (m_Leaderboards[i].Name == name)
                return m_Leaderboards[i];
        }
        return null;
    }

    public int GetCount()
    {
        return m_Leaderboards.Count;
    }

    public void SetUserTime(float userTime)
    {
        long ms = (long)(userTime * 1000.0f);       // Convert the time from seconds to milleseconds
        m_UserTime = (float)(ms) / 1000.0f;
    }
}
