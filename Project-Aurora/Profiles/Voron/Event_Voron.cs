﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using Aurora.EffectsEngine;
using Aurora.EffectsEngine.Animations;
using Aurora.EffectsEngine.Functions;
using Aurora.Profiles.Desktop;
using Aurora.Scripts.VoronScripts;

namespace Aurora.Profiles.Voron
{
	public class Event_Voron : LightEvent
	{

		public Event_Voron()
		{

		}

		static readonly CpuCoresUsage cpuCoresUsage = new CpuCoresUsage();

		public override void UpdateLights(EffectFrame frame)
		{
			Queue<EffectLayer> layers = new Queue<EffectLayer>();
			frame.AddLayers(cpuCoresUsage.UpdateLights(null));
			frame.AddLayers(layers.ToArray());
		}

		public override void UpdateLights(EffectFrame frame, GameState new_game_state)
		{
			//No need to do anything... This doesn't have any gamestates.
			UpdateLights(frame);
		}

		public override bool IsEnabled()
		{
			return true;
		}
	}
}