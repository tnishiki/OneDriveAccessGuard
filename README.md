# OneDrive AccessGuard

> 組織内のOneDrive共有設定を一元管理・監査するセキュリティツール

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

## 概要

OneDrive AccessGuard は、Microsoft 365 組織内のOneDriveに保存されたファイルの共有状態を可視化・管理するデスクトップアプリケーションです。不特定多数に公開されているファイルを自動検出し、管理者が安全に共有設定を変更・無効化できます。

## 主な機能

- 🔍 **全組織スキャン** — 全ユーザーのOneDriveを横断的にスキャン
- 🚨 **リスク分類** — 高/中/低/安全の4段階でリスクを自動判定
- 🔒 **共有無効化** — 匿名リンクや外部共有を個別・一括で削除
- 📊 **監査レポート** — CSV/Excel形式でのエクスポート
- 📝 **操作ログ** — 誰がいつどの設定を変更したかを記録

## 必要要件

| 要件 | 詳細 |
|------|------|
| OS | Windows 10 / 11 (64bit) |
| .NET | .NET 9.0 以上 |
| Microsoft 365 | E3 / E5 テナント |
| 実行権限 | グローバル管理者 または SharePoint 管理者 |

## セットアップ

### 1. Azure App Registration

1. [Azure Portal](https://portal.azure.com) → **Microsoft Entra ID** → **アプリの登録** → **新規登録**
2. 名前: `OneDriveAccessGuard`、サポートされるアカウントの種類: **この組織ディレクトリのみ**
3. **APIのアクセス許可** に以下を追加（アプリケーション権限）し、管理者の同意を付与：
   - `Files.Read.All`
   - `Files.ReadWrite.All`
   - `User.Read.All`
   - `Sites.Read.All`
4. **概要** から **アプリケーション (クライアント) ID** と **ディレクトリ (テナント) ID** をコピー

### 2. appsettings.json の設定

```json
{
  "AzureAd": {
    "ClientId": "取得したクライアントID",
    "TenantId": "取得したテナントID"
  }
}
```

> ⚠️ `appsettings.json` は `.gitignore` により除外されます。コミットしないよう注意してください。

### 3. ビルドと実行

```bash
git clone https://github.com/YOUR_ORG/OneDriveAccessGuard.git
cd OneDriveAccessGuard
dotnet build
dotnet run --project src/OneDriveAccessGuard.UI
```

## プロジェクト構成

```
OneDriveAccessGuard/
├── src/
│   ├── OneDriveAccessGuard.UI/           # WPF フロントエンド（MVVM）
│   ├── OneDriveAccessGuard.Core/         # ドメインモデル・インターフェース
│   └── OneDriveAccessGuard.Infrastructure/  # Graph API・DB・認証
├── tests/
│   └── OneDriveAccessGuard.Tests/        # xUnit テスト
└── docs/                                 # 設計資料
```

## セキュリティについて

- アクセストークンは **Windows DPAPI** で暗号化してローカルに保存
- ローカルDBは **AES-256** で暗号化
- すべての操作は監査ログに記録
- スキャン結果はローカルのみで処理（外部送信なし）

## ライセンス

MIT License — 詳細は [LICENSE](LICENSE) を参照してください。
