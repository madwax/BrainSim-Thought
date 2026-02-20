/*
 * Brain Simulator Thought
 *
 * Copyright (c) 2026 Charles Simon
 *
 * This file is part of Brain Simulator Thought and is licensed under
 * the MIT License. You may use, copy, modify, merge, publish, distribute,
 * sublicense, and/or sell copies of this software under the terms of
 * the MIT License.
 *
 * See the LICENSE file in the project root for full license information.
 */

using System;
using UKS;

namespace BrainSimulator.Modules;

public class ModuleWellBeing : ModuleBase
{
    private const float MinState = -1f;
    private const float MaxState = 1f;
    private static float _state = 0f; // -1 very sad, +1 very happy

    public static float State => _state;

    public static float SetState(float value)
    {
        _state = Math.Clamp(value, MinState, MaxState);
        return _state;
    }

    public static float Increase(float delta = 0.05f) => SetState(_state + delta);
    public static float Decrease(float delta = 0.05f) => SetState(_state - delta);

    public override void Fire()
    {
        Init();
        UpdateDialog();
    }

    public override void Initialize()
    {
        SetState(0f);
    }

    public override void UKSInitializedNotification()
    {
    }
}