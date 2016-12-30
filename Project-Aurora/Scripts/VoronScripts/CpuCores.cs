﻿//
// Voron Scripts - CpuCores
// v1.0-beta.3
// https://github.com/VoronFX/Aurora
// Copyright (C) 2016 Voronin Igor <Voron.exe@gmail.com>
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Aurora.Devices;
using Aurora.EffectsEngine;
using Aurora.Profiles;
using Aurora.Settings;

namespace Aurora.Scripts.VoronScripts
{

	public class CpuCores
	{
		public string ID = "CpuCores";

		public KeySequence DefaultKeys = new KeySequence();

		// Independant RainbowLoop
		public static ColorSpectrum RainbowLoop = new ColorSpectrum(
			Color.FromArgb(255, 0, 0),
			Color.FromArgb(255, 127, 0),
			Color.FromArgb(255, 255, 0),
			Color.FromArgb(0, 255, 0),
			Color.FromArgb(0, 0, 255),
			Color.FromArgb(75, 0, 130),
			Color.FromArgb(139, 0, 255),
			Color.FromArgb(255, 0, 0)
			);

		static readonly ColorSpectrum LoadGradient =
			new ColorSpectrum(Color.FromArgb(0, Color.Lime), Color.Lime, Color.Orange, Color.Red);

		static readonly ColorSpectrum BlinkingSpectrum =
			new ColorSpectrum(Color.Black, Color.FromArgb(0, Color.Black), Color.Black);

		private static int BlinkingSpeed = 1000;
		private static float BlinkingThreshold = 95;

		// Each key displays load of one core
		private static readonly DeviceKeys[] CpuCoresKeys =
			{ DeviceKeys.G6, DeviceKeys.G7, DeviceKeys.G8, DeviceKeys.G9 };

		// Keys for rainbow that represents processor usage by speed
		static readonly DeviceKeys[] RainbowCircleKeys = {
			DeviceKeys.NUM_ONE, DeviceKeys.NUM_FOUR, DeviceKeys.NUM_SEVEN, DeviceKeys.NUM_EIGHT,
			DeviceKeys.NUM_NINE, DeviceKeys.NUM_SIX, DeviceKeys.NUM_THREE, DeviceKeys.NUM_TWO
		};

		public EffectLayer[] UpdateLights(ScriptSettings settings, GameState state = null)
		{
			Queue<EffectLayer> layers = new Queue<EffectLayer>();

			var cpuLoad = Cpu.GetValue();
			var cpuOverload = (cpuLoad[0] - BlinkingThreshold) / 5f;
			cpuOverload = Math.Max(0, Math.Min(1, cpuOverload));

			EffectLayer CPULayer = new EffectLayer(ID + " - CPULayer", Color.FromArgb((byte)(255 * cpuOverload), Color.Black));
			EffectLayer CPULayerBlink = new EffectLayer(ID + " - CPULayerBlink");
			EffectLayer CPULayerRainbowCircle = new EffectLayer(ID + " - CPULayerRainbowCircle");

			var blinkColor = BlinkingSpectrum.GetColorAt((Utils.Time.GetMillisecondsSinceEpoch() % BlinkingSpeed) / (float)BlinkingSpeed);

			for (int i = 0; i < CpuCoresKeys.Length; i++)
			{
				CPULayer.Set(CpuCoresKeys[i], LoadGradient.GetColorAt(cpuLoad[i + 1] / 100f));
				CPULayerBlink.Set(CpuCoresKeys[i], Color.FromArgb((byte)(blinkColor.A * Math.Max(0, Math.Min(1,
					(cpuLoad[i + 1] - BlinkingThreshold) / 5))), blinkColor));
			}

			RainbowLoop.Shift((float)(-0.005 + -0.02 * cpuLoad[0] / 100f));

			for (int i = 0; i < RainbowCircleKeys.Length; i++)
			{
				CPULayerRainbowCircle.Set(RainbowCircleKeys[i],
					Color.FromArgb((byte)(255 * cpuLoad[0] / 100f),
					RainbowLoop.GetColorAt(i / (float)RainbowCircleKeys.Length)));
			}

			layers.Enqueue(CPULayer);
			layers.Enqueue(CPULayerBlink);
			layers.Enqueue(CPULayerRainbowCircle);

			return layers.ToArray();
		}

		private static readonly CpuPerCoreCounter Cpu = new CpuPerCoreCounter();

		internal class CpuPerCoreCounter : EasedPerformanceCounter<float[]>
		{
			private PerformanceCounter[] counters;
			private readonly float[] defaultValues = new float[Environment.ProcessorCount + 1];

			protected override float[] GetEasedValue(CounterFrame<float[]> currentFrame)
			{
				var prev = currentFrame.PreviousValue ?? defaultValues;
				var curr = currentFrame.CurrentValue ?? defaultValues;

				return prev.Select((x, i) => x + (curr[i] - x) * Math.Min(Utils.Time.GetMillisecondsSinceEpoch()
					- currentFrame.Timestamp, UpdateInterval) / UpdateInterval).ToArray();
			}

			protected override float[] UpdateValue()
			{
				if (counters == null)
					counters = new PerformanceCounter[Environment.ProcessorCount + 1]
						.Select((x, i) =>
						new PerformanceCounter("Processor", "% Processor Time", i == 0 ? "_Total" : (i - 1).ToString())).ToArray();

				return counters.Select(x => x.NextValue()).ToArray();
			}
		}

		internal abstract class EasedPerformanceCounter<T>
		{
			public int UpdateInterval { get; set; }
			public int IdleTimeout { get; set; }

			protected struct CounterFrame<T2>
			{
				public readonly T2 PreviousValue;
				public readonly T2 CurrentValue;
				public readonly long Timestamp;

				public CounterFrame(T2 previousValue, T2 currentValue)
				{
					PreviousValue = previousValue;
					CurrentValue = currentValue;
					Timestamp = Utils.Time.GetMillisecondsSinceEpoch();
				}
			}

			private CounterFrame<T> frame;
			private T lastEasedValue;

			private readonly Timer timer;
			private int counterUsage;
			private bool sleeping = true;
			private int awakening;

			protected abstract T GetEasedValue(CounterFrame<T> currentFrame);
			protected abstract T UpdateValue();

			public T GetValue(bool easing = true)
			{
				counterUsage = IdleTimeout;
				if (sleeping)
				{
					if (Interlocked.CompareExchange(ref awakening, 1, 0) == 1)
					{
						sleeping = false;
						timer.Change(0, Timeout.Infinite);
					}
				}

				if (easing)
				{
					lastEasedValue = GetEasedValue(frame);
					return lastEasedValue;
				}
				return frame.CurrentValue;
			}

			protected EasedPerformanceCounter()
			{
				UpdateInterval = 1000;
				IdleTimeout = 3;
				timer = new Timer(UpdateTick, null, Timeout.Infinite, Timeout.Infinite);
			}

			private void UpdateTick(object state)
			{
				try
				{
					frame = new CounterFrame<T>(lastEasedValue, UpdateValue());
				}
				catch (Exception exc)
				{
					Global.logger.LogLine("EasedPerformanceCounter exception: " + exc, Logging_Level.Error);
				}
				finally
				{
					counterUsage--;
					if (counterUsage <= 0)
					{
						awakening = 0;
						sleeping = true;
					}
					else
					{
						timer.Change(UpdateInterval, Timeout.Infinite);
					}
				}
			}
		}

	}

}