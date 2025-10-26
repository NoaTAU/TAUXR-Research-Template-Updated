using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

public class SessionManager : TXRSingleton<SessionManager>
{
    [SerializeField] private Round[] _rounds;
    private int _currentRound;

    //If there is a higher level flow manager, remove this and use his start method
    private void Start()
    {
        RunSessionFlow().Forget();
    }

    public async UniTask RunSessionFlow()
    {
        StartSession();

        while (_currentRound < _rounds.Length)
        {
            await RoundManager.Instance.RunRoundFlow(_rounds[_currentRound]);
            await BetweenRoundsFlow();
            _currentRound++;
        }

        EndSession();
    }


    private void StartSession()
    {
        // setup session initial conditions.
    }


    private void EndSession()
    {
        // setup end session conditions
    }

    private async UniTask BetweenRoundsFlow()
    {
        await UniTask.Yield();

        throw new NotImplementedException();
    }
}