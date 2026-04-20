# 員工帳號管理系統 API (MyOffice ACPD)

這是一個基於 .NET 8 Web API 實作的員工帳號管理系統，提供完整的 RESTful API 與 CRUD 功能，並包含資料庫自動產生 SID (20碼) 的進階設計。

## 🚀 技術堆疊
* **框架**: .NET 8 (ASP.NET Core Web API)
* **語言**: C# 12
* **資料庫**: Microsoft SQL Server 2025 Express
* **資料存取**: ADO.NET (Microsoft.Data.SqlClient) 搭配參數化查詢防範 SQL Injection
* **API 文件**: Swagger / OpenAPI

## 📁 專案架構
* `Controllers/MyOfficeAcpdController.cs`: 核心 API 控制器，實作 RESTful 路由。
* `DTOs/`: 包含 Request 與 Response 的 Data Transfer Objects，實現關注點分離。
* `DatabaseBackup/`: 包含 SQL Server 的 `.bak` 備份檔與初始建表腳本。

## ⚙️ 執行步驟

### 1. 資料庫還原
1. 開啟 SQL Server Management Studio (SSMS)。
2. 建立一個名為 `MyProject` 的空資料庫。
3. 執行「工作 > 還原 > 資料庫」，選擇專案目錄下的 `MyProject.bak` 進行還原。
4. 或直接執行提供的 SQL 腳本建立資料表與預存程序 `[dbo].[NEWSID]`。

### 2. 設定連線字串
請開啟專案中的 `appsettings.json`，根據你的本機環境修改 `DefaultConnection`：
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=你的伺服器名稱;Database=MyProject;Trusted_Connection=True;TrustServerCertificate=True;"
}
