// 🔴 从 ExpeditionFlow.cs 拆出（用户红线 <=600 行 · 架构 file_size_split §1.3 的 8 件计划）
//    本文件 = Reveal · **只搬家、零行为改动**（partial）✓
//    依赖主类私有成员（**实测扫描本文件得出**）：_claimedSecrets, _economy, _log, _tileWalker, _tuning

using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Core.Rng;
using Darkest.Data;
using Darkest.Gameplay.Sim.Director;   // 🆕 路线(c)：恢复件的类体用到它（HEAD 原先没有）✓
using Darkest.Gameplay.Sim.Survival;   // 🆕 事故回填：LightMeter 现居此命名空间（别人把它从 sim/run 搬来）✓

namespace Darkest.Gameplay.Sim.Run;

public sealed partial class ExpeditionFlow
{
	public int SecretRevealedCount => _claimedSecrets.Count;

	public int ScoutedTileCount => _tileWalker?.ScoutedCount ?? 0;

	public int RevealedTileCount => _tileWalker?.Revealed.Count ?? 0;

	public int VisitedTileCount => _tileWalker?.Visited.Count ?? 0;

	public int RevealSecrets(IEnumerable<(int X, int Y)> within)
	{
		TuningSecretScatter tuningSecretScatter = _tuning.DungeonLayer?.Secrets;
		if ((object)tuningSecretScatter != null && _tileWalker != null)
		{
			DungeonGridDeriver.Derived tw = TileWalk;
			if ((object)tw != null)
			{
				if (within == null)
				{
					return 0;
				}
				int maxRewardsPerRun = tuningSecretScatter.MaxRewardsPerRun;
				if (maxRewardsPerRun > 0 && _claimedSecrets.Count >= maxRewardsPerRun)
				{
					_log.Append(new EffectEvent(null, $"secret_quota_reached:{maxRewardsPerRun}", 0.0, Triggered: false));
					return 0;
				}
				int num = 0;
				foreach (var item in from p in within
					where tw.Grid.TileAt(p.X, p.Y) == DungeonTileKind.Secret
					orderby p.Y, p.X
					select p)
				{
					if (_claimedSecrets.Add(item))
					{
						_tileWalker.RevealScouted(new(int, int)[1] { item });
						int rewardGold = tuningSecretScatter.RewardGold;
						Economy economy = _economy;
						if (economy != null)
						{
							rewardGold = economy.AwardContent(_log, tuningSecretScatter.RewardGold, "secret");
						}
						else
						{
							rewardGold = 0;
							_log.Append(new EffectEvent(null, "secret_no_economy_gold_uncredited", 0.0, Triggered: false));
						}
						SecretGoldGranted += rewardGold;
						LastSecret = (item, rewardGold);
						num++;
						_log.Append(new EffectEvent(null, $"secret_revealed:{item.X},{item.Y}:gold{rewardGold}", 100.0, Triggered: true));
						if (maxRewardsPerRun > 0 && _claimedSecrets.Count >= maxRewardsPerRun)
						{
							break;
						}
					}
				}
				return num;
			}
		}
		return 0;
	}

	public int RevealSecretsWithinRooms(IEnumerable<int> roomIds)
	{
		DungeonGridDeriver.Derived tileWalk = TileWalk;
		if ((object)tileWalk == null)
		{
			return 0;
		}
		HashSet<int> set = new HashSet<int>(roomIds ?? Array.Empty<int>());
		if (set.Count == 0)
		{
			return 0;
		}
		return RevealSecrets(from kv in tileWalk.TileRoom
			where set.Contains(kv.Value)
			select kv.Key);
	}

	public RevealState TileStateAt((int X, int Y) pos)
	{
		return _tileWalker?.StateAt(pos, TileVision) ?? RevealState.Unexplored;
	}

	public bool VisitedTileAt((int X, int Y) pos)
	{
		return _tileWalker?.HasVisited(pos) ?? false;
	}

	public void RevealScoutedTiles(IEnumerable<(int X, int Y)> tiles)
	{
		if (_tileWalker != null && TileWalkEnabled)
		{
			_tileWalker.RevealScouted(tiles);
		}
	}

	public void RevealScoutedRooms(IEnumerable<int> roomIds)
	{
		if (_tileWalker == null)
		{
			return;
		}
		DungeonGridDeriver.Derived tileWalk = TileWalk;
		if ((object)tileWalk == null)
		{
			return;
		}
		HashSet<int> set = new HashSet<int>(roomIds ?? Array.Empty<int>());
		if (set.Count != 0)
		{
			RevealScoutedTiles(from kv in tileWalk.TileRoom
				where set.Contains(kv.Value)
				select kv.Key);
		}
	}
}
