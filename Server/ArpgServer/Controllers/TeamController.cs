using System.Security.Claims;
using ArpgServer.Data;
using ArpgServer.Models;
using ArpgServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace ArpgServer.Controllers;

/// <summary>
/// 组队控制器 — 创建房间/邀请/准备/踢出/转让
/// 路由: /api/team/*
/// </summary>
[ApiController]
[Route("api/team")]
[Authorize]
public class TeamController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RedisCacheService _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string GameServerUrl = "http://localhost:7778"; // Game Server HTTP端口
    private const int MaxTeamMembers = 5;

    public TeamController(AppDbContext db, RedisCacheService cache, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(claim!);
    }

    /// <summary>
    /// 开始副本 — 队长调用,请求Game Server创建房间
    /// POST /api/team/start?dungeonId=0
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartDungeon([FromQuery] int dungeonId)
    {
        int userId = GetUserId();

        // 获取当前队伍信息(从Redis读取)
        string teamKey = $"team:leader:{userId}";
        string teamJson = _cache.IsAvailable ? await _cache.GetAsync<string>(teamKey) : null;
        
        if (string.IsNullOrEmpty(teamJson))
            return BadRequest(new { error = "你不是队长或队伍不存在" });

        // 解析队伍成员
        var teamInfo = System.Text.Json.JsonSerializer.Deserialize<TeamInfo>(teamJson);
        if (teamInfo == null || teamInfo.Members.Count == 0)
            return BadRequest(new { error = "队伍没有成员" });

        // 检查所有成员是否已准备
        foreach (var member in teamInfo.Members)
        {
            if (!member.IsReady && member.UserId != userId)
                return Ok(new { success = false, error = $"{member.Username} 尚未准备" });
        }

        // 向Game Server发送创建房间请求
        var client = _httpClientFactory.CreateClient();
        var roomReq = new CreateRoomRequest
        {
            RoomId = System.Guid.NewGuid().ToString("N").Substring(0, 8),
            DungeonId = dungeonId,
            MemberUserIds = teamInfo.Members.Select(m => m.UserId).ToArray(),
            Difficulty = dungeonId + 1,
            MaxMembers = MaxTeamMembers
        };

        try
        {
            var response = await client.PostAsJsonAsync($"{GameServerUrl}/api/room/create", roomReq);
            if (!response.IsSuccessStatusCode)
                return StatusCode(500, new { error = "Game Server创建房间失败" });

            var roomResp = await response.Content.ReadFromJsonAsync<CreateRoomResponse>();
            if (roomResp == null || !roomResp.Success)
                return StatusCode(500, new { error = roomResp?.ErrorMessage ?? "创建房间失败" });

            // 将房间信息存入Redis, 供队员查询
            var roomData = new
            {
                roomId = roomResp.RoomId,
                port = roomResp.Port,
                dungeonId = dungeonId,
                members = teamInfo.Members,
                createdAt = DateTime.UtcNow
            };
            if (_cache.IsAvailable)
            {
                foreach (var member in teamInfo.Members)
                {
                    await _cache.SetAsync($"team:room:{member.UserId}",
                        System.Text.Json.JsonSerializer.Serialize(roomData),
                        TimeSpan.FromMinutes(30));
                }
            }

            return Ok(new
            {
                success = true,
                roomId = roomResp.RoomId,
                port = roomResp.Port,
                gameServerUrl = $"39.107.141.107:{roomResp.Port}"
            });
        }
        catch (Exception ex)
        {
            // Game Server可能未部署,回退到本地单人模式
            return Ok(new
            {
                success = true,
                fallback = true,
                message = "Game Server不可用,使用本地模式",
                roomId = roomReq.RoomId,
                port = 0
            });
        }
    }

    /// <summary>
    /// 获取当前房间信息 — 队员查询
    /// GET /api/team/room
    /// </summary>
    [HttpGet("room")]
    public async Task<ActionResult<object>> GetRoomInfo()
    {
        int userId = GetUserId();
        string roomKey = $"team:room:{userId}";

        if (_cache.IsAvailable)
        {
            var roomJson = await _cache.GetAsync<string>(roomKey);
            if (!string.IsNullOrEmpty(roomJson))
                return Ok(new { hasRoom = true, room = System.Text.Json.JsonSerializer.Deserialize<object>(roomJson) });
        }

        return Ok(new { hasRoom = false });
    }

    /// <summary>
    /// 接收Game Server推送的结算数据
    /// POST /api/team/complete
    /// </summary>
    [HttpPost("complete")]
    [AllowAnonymous]
    public async Task<IActionResult> TeamComplete([FromBody] SettlementData data)
    {
        if (data == null)
            return BadRequest(new { error = "无效的结算数据" });

        // 为每个玩家更新数据库
        foreach (var reward in data.PlayerRewards)
        {
            var save = await _db.PlayerSaves
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync(s => s.UserId == reward.PlayerId);

            if (save != null)
            {
                save.Gold += reward.GoldReward;
                if (data.Victory && data.DungeonId > save.HighestStageCleared)
                    save.HighestStageCleared = data.DungeonId;
                save.DataVersion++;
                save.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();

        // 清除房间缓存
        if (_cache.IsAvailable)
        {
            foreach (var reward in data.PlayerRewards)
                await _cache.RemoveAsync($"team:room:{reward.PlayerId}");
        }

        return Ok(new { success = true });
    }

    // ====== 内部类型 ======
    private class TeamInfo
    {
        public List<TeamMember> Members { get; set; } = new();
    }

    private class TeamMember
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public bool IsReady { get; set; }
        public bool IsLeader { get; set; }
    }
}
