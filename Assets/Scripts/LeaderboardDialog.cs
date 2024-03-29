// Copyright 2022 bitHeads, Inc. All Rights Reserved.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class LeaderboardDialog : Dialog
{
    [SerializeField] private LeaderboardRanking[] leaderboardRankings;
    [SerializeField] private Button leftSegmentButton;
    [SerializeField] private Button middleSegmentButton;
    [SerializeField] private Button rightSegmentButton;

    private enum SegmentControlState
    {
        Main = 0,
        Daily,
        Country
    };

    protected override void OnShow()
    {
        SetSegmentControlState(SegmentControlState.Main);
        SetLeaderboardData(Constants.kBrainCloudMainLeaderboardID);
    }

    public void OnMainScoresClicked()
    {
        SetSegmentControlState(SegmentControlState.Main);
        SetLeaderboardData(Constants.kBrainCloudMainLeaderboardID);
    }

    public void OnDailyScoresClicked()
    {
        SetSegmentControlState(SegmentControlState.Daily);
        SetLeaderboardData(Constants.kBrainCloudDailyLeaderboardID);
    }

    public void OnCountryScoresClicked()
    {
        SetSegmentControlState(SegmentControlState.Country);
        SetLeaderboardData(Constants.kBrainCloudCountryLeaderboardID);
    }

    private void SetSegmentControlState(SegmentControlState segmentControlState)
    {
        string leftTexture = GetTextureFile(SegmentControlState.Main, segmentControlState == SegmentControlState.Main);
        string middleTexture = GetTextureFile(SegmentControlState.Daily, segmentControlState == SegmentControlState.Daily);
        string rightTexture = GetTextureFile(SegmentControlState.Country, segmentControlState == SegmentControlState.Country);

        leftSegmentButton.image.sprite = Resources.Load<Sprite>(leftTexture);
        middleSegmentButton.image.sprite = Resources.Load<Sprite>(middleTexture);
        rightSegmentButton.image.sprite = Resources.Load<Sprite>(rightTexture);

        Color32 selectedColor = new Color32(255, 255, 255, 255);
        Color32 unselectedColor = new Color32(255, 255, 255, 255);

        leftSegmentButton.GetComponentInChildren<TMPro.TMP_Text>().color = segmentControlState == SegmentControlState.Main ? selectedColor : unselectedColor;
        middleSegmentButton.GetComponentInChildren<TMPro.TMP_Text>().color = segmentControlState == SegmentControlState.Daily ? selectedColor : unselectedColor;
        rightSegmentButton.GetComponentInChildren<TMPro.TMP_Text>().color = segmentControlState == SegmentControlState.Country ? selectedColor : unselectedColor;
    }

    private void SetLeaderboardData(string leaderboardId)
    {
        LeaderboardEntry leaderboardEntry;

        ResetLeaderboardData();

        Leaderboard leaderboard = LeaderboardsManager.sharedInstance.GetLeaderboardByName(leaderboardId);

        if (leaderboard != null)
        {
            for (int i = 0; i < leaderboard.GetCount(); i++)
            {
                leaderboardEntry = leaderboard.GetLeaderboardEntryAtIndex(i);
                if (leaderboardEntry != null && i < leaderboardRankings.Length)
                    leaderboardRankings[i].Set(leaderboardEntry);
            }
        }
    }

    private void ResetLeaderboardData()
    {
        foreach (LeaderboardRanking hsr in leaderboardRankings)
            hsr.Reset();
    }

    private string GetTextureFile(SegmentControlState segmentControlState, bool isSelected)
    {
        string texture = "Textures/SegmentControl";

        switch (segmentControlState)
        {
            case SegmentControlState.Main:
                texture += "Left-";
                break;
            case SegmentControlState.Daily:
                texture += "Middle-";
                break;
            case SegmentControlState.Country:
                texture += "Right-";
                break;
        }

        texture += isSelected ? "Selected" : "Unselected";
        return texture;
    }
}