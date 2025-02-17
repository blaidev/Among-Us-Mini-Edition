﻿using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Net;
using System.Text;
using System.IO;
//BepInEx stuff
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
//more stuff
using System.Collections.Generic;
using System.Collections;
using System.Runtime.Serialization.Formatters.Binary;
using AmongUsCEDE.Core;
using AmongUsCEDE.Core.Extensions;
using AmongUsCEDE.Mods;
using MoonSharp.Interpreter;
using AmongUsCEDE.Core.CustomSettings;

namespace AmongUsCEDE
{

	public enum LuaComplete //i literally created all of these enums for nothing
	{
		Success, //The LUA script ran succesfully
		Crash, //Both the LUA script and the base LUA script crashed.
		Error, //The original script errored, however, the base succeeded.
		Missing, //The requested function is missing from the LUA, however base has it.
		NonExistant //Neither script has it.
	}

	public enum VictoryTypes
	{
		Vanilla,
		Stalemate,
		Tie,
		Error
	}


	public class AdditionalWinningInfo
	{
		public Color RoleColor;


		public AdditionalWinningInfo(GameData.PlayerInfo playfo)
		{
			RoleColor = playfo.GetRole().RoleColor;
		}
	}

	public static class CETempData
	{
		public static string End_Song = "default_crewmate";
		public static VictoryTypes VictoryType = VictoryTypes.Vanilla;
		public static List<AdditionalWinningInfo> AdditionalWinningInfos = new List<AdditionalWinningInfo>();
	}


	public static class CEManager //wanted to make this a monobehavior but unity wouldn't cooperate
	{
		public static bool ModsLoaded = false;

		public static int Colors_Page = 0;

		public static List<Setting> HardcodedSettings = new List<Setting>()
		{
			new StringListSetting("intern_gamemode","Gamemode",0,new string[255],1)
		};
		

		public static Role[] AllRoles //TODO: Find out if this is crazy ineffecient or something
		{
			get
			{
				List<Role> Roles = new List<Role>();
				for (int i = 0; i < ModLoader.Mods.Count; i++)
				{
					for (int h = 0; h < ModLoader.Mods[i].Gamemodes.Count; h++)
					{
						for (int j = 0; j < ModLoader.Mods[i].Gamemodes[h].Roles.Count; h++)
						{
							Roles.Add(ModLoader.Mods[i].Gamemodes[h].Roles[j]);
						}
					}
				}
				return Roles.ToArray();
			}
		}

		public static int GlobalGamemodeCount
		{
			get
			{
				int gamemodes = 0;
				for (int i = 0; i < ModLoader.Mods.Count; i++)
				{
					gamemodes += ModLoader.Mods[i].Gamemodes.Count;
				}
				return gamemodes;
			}
		}
	
		
	}

	public static class ScriptManager
	{

		public static Gamemode CurrentGamemode
		{
			get
			{
				if (GameOptionsExtension.Gamemode >= ModLoader.Mods[0].Gamemodes.Count)
				{
					UnityEngine.Debug.Log("gamemode out of range! " + GameOptionsExtension.Gamemode);
					return ModLoader.Mods[0].Gamemodes[0];
				}
				return ModLoader.Mods[0].Gamemodes[GameOptionsExtension.Gamemode];
			}
		}



		public static bool RanSucessfully(this LuaComplete me)
		{
			return me == LuaComplete.Success || me == LuaComplete.Missing || me == LuaComplete.Error;
		}

		public static DynValue CallCurrentGMHooks(string hook, params object[] parm)
		{
			DynValue val = DynValue.Nil;

			if (CurrentGamemode.Hooks.TryGetValue(hook, out CodeHook curhook))
			{
				try
				{
					switch (curhook.Language)
					{
						case ScriptLanguage.Lua:
							val = (curhook.HookObject as Closure).Call(parm);
							break;
					}
				}
				catch (ScriptRuntimeException E)
				{
					UnityEngine.Debug.Log("Caught Hook Error(" + hook + "):" + E.DecoratedMessage);
					return null;
				}
			}


			if (val.Type == DataType.Nil)
			{
				return null;
			}

			return val;
		}


		public static DynValue RunCurrentGMFunction(string function, bool DisallowOverride, params object[] parm)
		{
			CodeScript cs = CurrentGamemode.Script;
			DynValue hookdyn = CallCurrentGMHooks(function, parm);
			DynValue dyn = DynValue.Nil;

			if (hookdyn != null && !DisallowOverride)
			{
				return hookdyn;
			}


			switch (cs.Language)
			{
				case ScriptLanguage.Lua:
					Script scr = cs.Script as Script;
					try
					{
						dyn = scr.Call(scr.Globals[function], parm);
					}
					catch (Exception E)
					{
						throw new CriticalClientException("Caught Lua Error(" + function + "):" + E.Message);
					}
					break;
				default:
					throw new Exception("Unimplemented ScriptType:" + cs.Language.ToString());
			}


			if (dyn.Type == DataType.Nil)
			{
				return null;
			}

			return dyn;
		}
	}
}
