using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Data;
using System.Text.Json;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/myofficeacpd")]
    public class MyOfficeAcpdController : ControllerBase
    {
        private readonly ILogger<MyOfficeAcpdController> _logger;
        private readonly string _connectionString;

        public MyOfficeAcpdController(ILogger<MyOfficeAcpdController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                              ?? throw new InvalidOperationException("找不到資料庫連線字串");
        }

        // ==========================================
        //  共用方法：寫入資料庫 Log
        // ==========================================
        private async Task WriteLogAsync(Guid groupId, string actionName, object? actionData, SqlConnection connection)
        {
            try
            {
                using (var cmdLog = new SqlCommand("[dbo].[usp_AddLog]", connection))
                {
                    cmdLog.CommandType = CommandType.StoredProcedure;
                    cmdLog.Parameters.AddWithValue("@_InBox_ReadID", 0);
                    cmdLog.Parameters.AddWithValue("@_InBox_SPNAME", "MyOfficeAcpdController");
                    cmdLog.Parameters.AddWithValue("@_InBox_GroupID", groupId);
                    cmdLog.Parameters.AddWithValue("@_InBox_ExProgram", actionName);

                    string jsonInfo = actionData != null ? JsonSerializer.Serialize(actionData) : "{}";
                    cmdLog.Parameters.AddWithValue("@_InBox_ActionJSON", jsonInfo);

                    var returnValuesParam = new SqlParameter { ParameterName = "@_OutBox_ReturnValues", SqlDbType = SqlDbType.NVarChar, Size = -1, Direction = ParameterDirection.Output };
                    cmdLog.Parameters.Add(returnValuesParam);

                    await cmdLog.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "寫入資料庫 Log 失敗");
            }
        }

        // ==========================================
        // 1. 查詢所有資料 (GET)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var accountList = new List<AccountResponse>();
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string querySql = "SELECT [ACPD_SID], [ACPD_Cname], [ACPD_Ename], [ACPD_Email], [ACPD_Status] FROM [dbo].[MyOffice_ACPD] ORDER BY [ACPD_NowDateTime] DESC";

                    using (var cmd = new SqlCommand(querySql, connection))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            accountList.Add(new AccountResponse
                            {
                                SID = reader["ACPD_SID"].ToString(),
                                Cname = reader["ACPD_Cname"] != DBNull.Value ? reader["ACPD_Cname"].ToString() : null,
                                Ename = reader["ACPD_Ename"] != DBNull.Value ? reader["ACPD_Ename"].ToString() : null,
                                Email = reader["ACPD_Email"] != DBNull.Value ? reader["ACPD_Email"].ToString() : null,
                                Status = reader["ACPD_Status"] != DBNull.Value ? Convert.ToInt32(reader["ACPD_Status"]) : 0
                            });
                        }
                    }
                }
                return Ok(accountList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查詢列表失敗");
                return StatusCode(500, new { Message = "伺服器內部錯誤" });
            }
        }

        // ==========================================
        // 2. 查詢單筆資料 (GET) 
        // (這就是 CS0103 報錯說找不到的那個方法！)
        // ==========================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { Message = "ID 不可為空" });

            try
            {
                AccountResponse? account = null;
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string querySql = "SELECT [ACPD_SID], [ACPD_Cname], [ACPD_Ename], [ACPD_Email], [ACPD_Status] FROM [dbo].[MyOffice_ACPD] WHERE [ACPD_SID] = @SID";

                    using (var cmd = new SqlCommand(querySql, connection))
                    {
                        cmd.Parameters.AddWithValue("@SID", id);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                account = new AccountResponse
                                {
                                    SID = reader["ACPD_SID"].ToString(),
                                    Cname = reader["ACPD_Cname"] != DBNull.Value ? reader["ACPD_Cname"].ToString() : null,
                                    Ename = reader["ACPD_Ename"] != DBNull.Value ? reader["ACPD_Ename"].ToString() : null,
                                    Email = reader["ACPD_Email"] != DBNull.Value ? reader["ACPD_Email"].ToString() : null,
                                    Status = reader["ACPD_Status"] != DBNull.Value ? Convert.ToInt32(reader["ACPD_Status"]) : 0
                                };
                            }
                        }
                    }
                }
                if (account == null) return NotFound(new { Message = "資源不存在" });
                return Ok(account);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"查詢 ID: {id} 失敗");
                return StatusCode(500, new { Message = "伺服器內部錯誤" });
            }
        }

        // ==========================================
        // 3. 新增資料 (POST) - 結合自動 Log 機制
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAccountRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            Guid currentGroupId = Guid.NewGuid(); // 產生此次交易的專屬追蹤碼

            try
            {
                string newSid = string.Empty;
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    await WriteLogAsync(currentGroupId, "1. 開始建立帳號", request, connection);

                    // 呼叫預存程序產生 SID
                    using (var cmdSid = new SqlCommand("[dbo].[NEWSID]", connection))
                    {
                        cmdSid.CommandType = CommandType.StoredProcedure;
                        cmdSid.Parameters.AddWithValue("@TableName", "MyOffice_ACPD");
                        var returnSidParam = new SqlParameter { ParameterName = "@ReturnSID", SqlDbType = SqlDbType.NVarChar, Size = 20, Direction = ParameterDirection.Output };
                        cmdSid.Parameters.Add(returnSidParam);
                        await cmdSid.ExecuteNonQueryAsync();
                        newSid = returnSidParam.Value?.ToString() ?? "";
                    }

                    if (string.IsNullOrEmpty(newSid))
                    {
                        await WriteLogAsync(currentGroupId, "Error: SID 產生失敗", null, connection);
                        return StatusCode(500, new { Message = "無法產生 SID" });
                    }

                    await WriteLogAsync(currentGroupId, "2. SID 產生成功", new { GeneratedSID = newSid }, connection);

                    // 新增資料
                    string insertSql = @"
                        INSERT INTO [dbo].[MyOffice_ACPD] 
                        ([ACPD_SID], [ACPD_Cname], [ACPD_Ename], [ACPD_Email], [ACPD_LoginID], [ACPD_LoginPWD], [ACPD_NowID], [ACPD_UPDID]) 
                        VALUES (@SID, @Cname, @Ename, @Email, @LoginID, @LoginPWD, @NowID, @UpdID)";

                    using (var cmdInsert = new SqlCommand(insertSql, connection))
                    {
                        cmdInsert.Parameters.AddWithValue("@SID", newSid);
                        cmdInsert.Parameters.AddWithValue("@Cname", request.Cname ?? (object)DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@Ename", request.Ename ?? (object)DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@Email", request.Email ?? (object)DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@LoginID", request.LoginID ?? (object)DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@LoginPWD", request.LoginPWD ?? (object)DBNull.Value);
                        cmdInsert.Parameters.AddWithValue("@NowID", "SystemAPI");
                        cmdInsert.Parameters.AddWithValue("@UpdID", "SystemAPI");

                        await cmdInsert.ExecuteNonQueryAsync();
                    }

                    await WriteLogAsync(currentGroupId, "3. 帳號建立完成", new { FinalSID = newSid }, connection);
                }

                return CreatedAtAction(nameof(GetById), new { id = newSid }, new { Message = "建立成功", SID = newSid });
            }
            catch (Exception ex)
            {
                using (var errorConn = new SqlConnection(_connectionString))
                {
                    await errorConn.OpenAsync();
                    await WriteLogAsync(currentGroupId, "Error: 發生例外狀況", new { ErrorMessage = ex.Message }, errorConn);
                }
                _logger.LogError(ex, "新增資料失敗");
                return StatusCode(500, new { Message = "伺服器內部錯誤" });
            }
        }

        // ==========================================
        // 4. 更新資料 (PUT)
        // ==========================================
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateAccountRequest request)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { Message = "ID 不可為空" });

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string updateSql = @"
                        UPDATE [dbo].[MyOffice_ACPD] 
                        SET [ACPD_Cname] = @Cname, [ACPD_Ename] = @Ename, [ACPD_Email] = @Email, [ACPD_Status] = @Status,
                            [ACPD_UPDDateTime] = GETDATE(), [ACPD_UPDID] = 'SystemAPI'
                        WHERE [ACPD_SID] = @SID";

                    using (var cmd = new SqlCommand(updateSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@SID", id);
                        cmd.Parameters.AddWithValue("@Cname", request.Cname ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Ename", request.Ename ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Email", request.Email ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Status", request.Status ?? (object)DBNull.Value);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        if (rowsAffected == 0) return NotFound(new { Message = "資源不存在" });
                    }
                }
                return Ok(new { Message = "更新成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新 ID: {id} 失敗");
                return StatusCode(500, new { Message = "伺服器內部錯誤" });
            }
        }

        // ==========================================
        // 5. 刪除資料 (DELETE)
        // ==========================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest(new { Message = "ID 不可為空" });

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string deleteSql = "DELETE FROM [dbo].[MyOffice_ACPD] WHERE [ACPD_SID] = @SID";

                    using (var cmd = new SqlCommand(deleteSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@SID", id);
                        int rowsAffected = await cmd.ExecuteNonQueryAsync();
                        if (rowsAffected == 0) return NotFound(new { Message = "資源不存在" });
                    }
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"刪除 ID: {id} 失敗");
                return StatusCode(500, new { Message = "伺服器內部錯誤" });
            }
        }
    }

    public class AccountResponse
    {
        public string? SID { get; set; }
        public string? Cname { get; set; }
        public string? Ename { get; set; }
        public string? Email { get; set; }
        public int Status { get; set; }
    }

    public class CreateAccountRequest
    {
        [DefaultValue("測試帳號")]
        public string? Cname { get; set; }

        [DefaultValue("Test User")]
        public string? Ename { get; set; }

        [DefaultValue("testuser@example.com")]
        public string? Email { get; set; }

        [DefaultValue("admin_test")]
        public string? LoginID { get; set; }

        [DefaultValue("Password123!")]
        public string? LoginPWD { get; set; }
    }

    public class UpdateAccountRequest
    {
        [DefaultValue("更新後的名稱")]
        public string? Cname { get; set; }

        [DefaultValue("Updated User")]
        public string? Ename { get; set; }

        [DefaultValue("updated@example.com")]
        public string? Email { get; set; }

        [DefaultValue(1)]
        public byte? Status { get; set; }
    }
}