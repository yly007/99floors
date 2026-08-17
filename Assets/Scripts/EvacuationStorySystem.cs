using System.Collections.Generic;

namespace NinetyNine
{
    public enum StoryAct
    {
        Trapped,
        Remembered,
        Witnessed
    }

    public sealed class StoryClue
    {
        public string Id { get; }
        public string Title { get; }
        public string Excerpt { get; }

        public StoryClue(string id, string title, string excerpt)
        {
            Id = id;
            Title = title;
            Excerpt = excerpt;
        }
    }

    public enum ExitResolution
    {
        FalseLoop,
        EscapedAlone,
        ShutDownBuilding,
        NewAdministrator,
        MimicTakeover
    }

    public sealed class EvacuationStorySystem
    {
        public const int MinimumExitClues = 3;
        public const int TrueExitClues = 6;

        private readonly HashSet<string> _clues = new HashSet<string>();
        private readonly List<StoryClue> _records = new List<StoryClue>();

        public int ClueCount => _clues.Count;
        public StoryAct Act => _clues.Count >= 5
            ? StoryAct.Witnessed
            : _clues.Count >= 2 ? StoryAct.Remembered : StoryAct.Trapped;
        public StoryClue LatestClue => _records.Count > 0 ? _records[_records.Count - 1] : null;
        public IReadOnlyList<StoryClue> Records => _records;

        public bool Discover(string clueId)
        {
            if (string.IsNullOrEmpty(clueId) || !_clues.Add(clueId)) return false;
            _records.Add(CreateRecord(clueId));
            return true;
        }

        public void Reset()
        {
            _clues.Clear();
            _records.Clear();
        }

        public string ActTitle()
        {
            if (Act == StoryAct.Witnessed) return "第三幕 · 交接";
            if (Act == StoryAct.Remembered) return "第二幕 · 被抹去的人";
            return "第一幕 · 不存在的电梯";
        }

        private static StoryClue CreateRecord(string clueId)
        {
            int variant = StableIndex(clueId, 3);
            string lower = clueId.ToLowerInvariant();
            if (lower.Contains("witness") || lower.Contains("contradiction"))
            {
                string[] excerpts =
                {
                    "证人说，电梯每带走一个人，大楼就会补上一层。",
                    "证人记得自己已经死过一次；员工表却仍把他列为在岗。",
                    "证人警告：一楼只会为能说出真相的人打开。"
                };
                return new StoryClue(clueId, "幸存者证词", excerpts[variant]);
            }
            if (lower.Contains("phone"))
            {
                string[] excerpts =
                {
                    "电话录音：午夜封锁不是为了关门，而是为了重置目击者。",
                    "电话另一端反复念着同一串工号——那是我的工号。",
                    "一段未来留言：不要相信已经抵达一楼的提示。"
                };
                return new StoryClue(clueId, "未接来电录音", excerpts[variant]);
            }
            if (lower.Contains("passengermismatch") || lower.Contains("unsyncedshadow"))
            {
                string[] excerpts =
                {
                    "监控截图里，轿厢始终比登记人数多出一人。",
                    "影像记录表明：大楼会用熟悉的脸替换失踪员工。",
                    "镜面中的乘客没有工号，也没有离开记录。"
                };
                return new StoryClue(clueId, "异常监控记录", excerpts[variant]);
            }
            if (lower.Contains("falselobby") || lower.Contains("wrongfloornumber"))
            {
                string[] excerpts =
                {
                    "施工图上共有九十八层；第九十九层只存在于电梯控制程序。",
                    "所谓一楼大厅，是大楼筛选下一任管理员的最后一间办公室。",
                    "楼层编号并非位置，而是大楼对记忆进行归档的序号。"
                };
                return new StoryClue(clueId, "被涂改的建筑图", excerpts[variant]);
            }

            string[] archiveExcerpts =
            {
                "档案显示，午夜后失踪的员工都会被改写为“从未入职”。",
                "停电事故报告被重复签署了九十九次，日期却完全相同。",
                "管理员备注：备用电池只负责下降；真正锁住出口的是未被记录的目击者。"
            };
            return new StoryClue(clueId, "封锁档案残页", archiveExcerpts[variant]);
        }

        private static int StableIndex(string value, int count)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return (hash & int.MaxValue) % count;
            }
        }

        public ExitResolution Resolve(bool carriesMimic, int rescued, bool acceptedAdministrator)
        {
            if (carriesMimic) return ExitResolution.MimicTakeover;
            if (acceptedAdministrator) return ExitResolution.NewAdministrator;
            if (_clues.Count < MinimumExitClues) return ExitResolution.FalseLoop;
            if (_clues.Count >= TrueExitClues && rescued >= 1 && !carriesMimic)
            {
                return ExitResolution.ShutDownBuilding;
            }
            return ExitResolution.EscapedAlone;
        }
    }
}
