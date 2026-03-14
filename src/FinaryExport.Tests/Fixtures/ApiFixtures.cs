using System.Net;

namespace FinaryExport.Tests.Fixtures;

// Sample JSON response fixtures based on api-analysis.md response schemas.
// Used as mock HTTP response bodies throughout the test suite.
public static class ApiFixtures
{
	// --- Auth Fixtures ---

	public static string ClerkEnvironmentResponse => """
    {
        "auth_config": {
            "identification_strategies": ["email_address", "oauth_apple", "oauth_google"],
            "second_factors": ["backup_code", "totp"],
            "single_session_mode": true
        },
        "display_config": {
            "instance_environment_type": "production"
        }
    }
    """;

	public static string ClerkClientResponse => """
    {
        "object": "client",
        "id": "client_test_123",
        "sessions": [],
        "sign_in": null,
        "sign_up": null,
        "last_active_session_id": null
    }
    """;

	public static string ClerkSignInResponse(string signInId = "sia_test_abc123") => $$"""
    {
        "object": "sign_in",
        "id": "{{signInId}}",
        "status": "needs_second_factor",
        "first_factor_verification": {
            "status": "verified",
            "strategy": "password"
        },
        "second_factor_verification": null
    }
    """;

	public static string ClerkSignInInvalidPasswordResponse => """
    {
        "errors": [
            {
                "code": "form_password_incorrect",
                "message": "Password is incorrect. Try again, or use another method.",
                "long_message": "Password is incorrect."
            }
        ]
    }
    """;

	public static string ClerkSecondFactorResponse(string sessionId = "sess_test_xyz789") => $$"""
    {
        "object": "sign_in",
        "id": "sia_test_abc123",
        "status": "complete",
        "created_session_id": "{{sessionId}}",
        "second_factor_verification": {
            "status": "verified",
            "strategy": "totp"
        }
    }
    """;

	public static string ClerkInvalidTotpResponse => """
    {
        "errors": [
            {
                "code": "form_code_incorrect",
                "message": "Incorrect code. Please try again.",
                "long_message": "Incorrect code."
            }
        ]
    }
    """;

	public static string ClerkSessionTouchResponse(string sessionId = "sess_test_xyz789") => $$"""
    {
        "object": "session",
        "id": "{{sessionId}}",
        "status": "active",
        "expire_at": "2026-06-12T09:00:00.000Z",
        "last_active_token": {
            "object": "token",
            "jwt": "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJ1c2VyX3Rlc3QiLCJzaWQiOiJzZXNzX3Rlc3RfeHl6Nzg5IiwiZXhwIjoxNzQxNzc2MDYwLCJpYXQiOjE3NDE3NzYwMDB9.fake_signature"
        }
    }
    """;

	public static string ClerkTokenResponse(string jwt = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJ1c2VyX3Rlc3QiLCJzaWQiOiJzZXNzX3Rlc3RfeHl6Nzg5IiwiZXhwIjoxNzQxNzc2MDYwLCJpYXQiOjE3NDE3NzYwMDB9.fake_signature") => $$"""
    {
        "object": "token",
        "jwt": "{{jwt}}"
    }
    """;

	// --- Finary API Fixtures ---

	public static string OrganizationsResponse => """
    {
        "result": [
            {
                "id": "org_test_123",
                "name": "My Wealth",
                "members": [
                    {
                        "id": "membership_test_456",
                        "member_type": "owner",
                        "user": { "fullname": "Jean Dupont", "is_organization_owner": true }
                    },
                    {
                        "id": "membership_test_789",
                        "member_type": "User",
                        "user": { "fullname": "Marie Dupont", "is_organization_owner": false }
                    }
                ]
            }
        ],
        "message": null,
        "error": null
    }
    """;

	public static string PortfolioSummaryResponse => """
    {
        "result": {
            "created_at": "2024-01-15T10:30:00Z",
            "gross": {
                "total": {
                    "amount": 150000.50,
                    "display_amount": 150000.50,
                    "evolution": 1200.00,
                    "period_evolution": -500.00,
                    "display_upnl_difference": 1200.00,
                    "display_value_difference": -500.00,
                    "share": 100.0,
                    "evolution_percent": 0.81,
                    "period_evolution_percent": -0.33,
                    "display_upnl_percent": 0.81,
                    "display_value_evolution": -0.33
                },
                "assets": {},
                "liabilities": {}
            },
            "net": {
                "total": {
                    "amount": 142000.25,
                    "display_amount": 142000.25,
                    "evolution": 1100.00,
                    "period_evolution": -450.00,
                    "display_upnl_difference": 1100.00,
                    "display_value_difference": -450.00,
                    "share": 100.0,
                    "evolution_percent": 0.78,
                    "period_evolution_percent": -0.32,
                    "display_upnl_percent": 0.78,
                    "display_value_evolution": -0.32
                },
                "assets": {},
                "liabilities": {}
            },
            "finary": {
                "total": {
                    "amount": 0.00,
                    "display_amount": 0.00,
                    "evolution": 0.00,
                    "period_evolution": 0.00,
                    "display_upnl_difference": 0.00,
                    "display_value_difference": 0.00,
                    "share": 0.0,
                    "evolution_percent": 0.00,
                    "period_evolution_percent": 0.00,
                    "display_upnl_percent": 0.00,
                    "display_value_evolution": 0.00
                },
                "assets": {},
                "liabilities": {}
            },
            "has_unqualified_loans": false,
            "has_unlinked_loans": false
        },
        "message": null,
        "error": null
    }
    """;

	public static string CategoryAccountsResponse => """
    {
        "result": [
            {
                "slug": "checking-bnp-main",
                "name": "BNP Main Checking",
                "connection_id": "conn_aaa111",
                "state": "active",
                "state_message": null,
                "correlation_id": "corr_bbb222",
                "iban": "FR7630004000031234567890143",
                "id": "acct_ccc333",
                "manual_type": "bank",
                "logo_url": "https://cdn.finary.com/logos/bnp.png",
                "created_at": "2023-06-01T08:00:00Z",
                "annual_yield": 0.0,
                "balance": 4523.67,
                "display_balance": 4523.67,
                "organization_balance": 4523.67,
                "display_organization_balance": 4523.67,
                "buying_value": 0.0,
                "display_buying_value": 0.0,
                "last_sync_at": "2024-03-12T00:36:19.165Z",
                "is_manual": false,
                "currency": {
                    "code": "EUR",
                    "symbol": "\u20ac",
                    "name": "Euro"
                },
                "institution": {
                    "id": "inst_001",
                    "slug": "bnp-paribas",
                    "name": "BNP Paribas"
                }
            },
            {
                "slug": "checking-sg-joint",
                "name": "SG Joint Account",
                "connection_id": "conn_ddd444",
                "state": "active",
                "state_message": null,
                "correlation_id": "corr_eee555",
                "iban": "FR7630003000021234567890167",
                "id": "acct_fff666",
                "manual_type": "bank",
                "logo_url": "https://cdn.finary.com/logos/sg.png",
                "created_at": "2023-09-15T10:00:00Z",
                "annual_yield": 0.0,
                "balance": 1205.30,
                "display_balance": 1205.30,
                "organization_balance": 602.65,
                "display_organization_balance": 602.65,
                "buying_value": 0.0,
                "display_buying_value": 0.0,
                "last_sync_at": "2024-03-11T23:00:00Z",
                "is_manual": false,
                "currency": {
                    "code": "EUR",
                    "symbol": "\u20ac",
                    "name": "Euro"
                },
                "institution": {
                    "id": "inst_002",
                    "slug": "societe-generale",
                    "name": "Soci\u00e9t\u00e9 G\u00e9n\u00e9rale"
                }
            }
        ],
        "message": null,
        "error": null
    }
    """;

	public static string CategoryTransactionsPageResponse(int count) =>
		$$"""
        {
            "result": [
                {{string.Join(",\n", Enumerable.Range(1, count).Select(i => $$"""
                {
                    "name": "Transaction {{i}}",
                    "simplified_name": "transaction_{{i}}",
                    "display_name": "Transaction {{i}}",
                    "correlation_id": "corr_txn_{{i}}",
                    "date": "2024-03-{{i:D2}}T00:00:00.000Z",
                    "display_date": "2024-03-{{i:D2}}",
                    "value": {{(i % 2 == 0 ? -50.0m * i : 100.0m * i)}},
                    "display_value": {{(i % 2 == 0 ? -50.0m * i : 100.0m * i)}},
                    "id": {{3000000000L + i}},
                    "transaction_type": "{{(i % 2 == 0 ? "expense" : "income")}}",
                    "commission": null,
                    "external_id_category": 9998,
                    "currency": {
                        "code": "EUR",
                        "symbol": "\u20ac",
                        "name": "Euro"
                    },
                    "institution": {
                        "name": "Test Bank",
                        "slug": "test-bank"
                    },
                    "account": {
                        "id": "acct_{{i}}",
                        "name": "Test Account {{i}}",
                        "slug": "test-account-{{i}}"
                    },
                    "include_in_analysis": true,
                    "is_internal_transfer": false,
                    "marked": false
                }
                """))}}
            ],
            "message": null,
            "error": null
        }
        """;

	public static string EmptyListResponse => """
    {
        "result": [],
        "message": null,
        "error": null
    }
    """;

	public static string TimeseriesResponse => """
    {
        "result": {
            "label": "Checkings",
            "period_evolution_percent": 2.35,
            "timeseries": [
                ["2024-01-01T00:00:00.000Z", 3000.00],
                ["2024-02-01T00:00:00.000Z", 3200.50],
                ["2024-03-01T00:00:00.000Z", 4523.67]
            ],
            "period_evolution": 1523.67,
            "display_amount": 4523.67,
            "display_value_difference": 1523.67,
            "display_value_evolution": 50.79,
            "balance": 4523.67
        },
        "message": null,
        "error": null
    }
    """;

	public static string DividendsResponse => """
    {
        "result": {
            "annual_income": 3500.00,
            "past_income": 2800.00,
            "next_year": [
                { "date": "2026-04-01T00:00:00.000Z", "value": 925.00 },
                { "date": "2026-07-01T00:00:00.000Z", "value": 925.00 },
                { "date": "2026-10-01T00:00:00.000Z", "value": 925.00 },
                { "date": "2027-01-01T00:00:00.000Z", "value": 925.00 }
            ],
            "yield": 2.33
        },
        "message": null,
        "error": null
    }
    """;

	public static string AllocationResponse => """
    {
        "result": {
            "total": 150000.50,
            "share": 100.0,
            "distribution": [
                { "label": "France", "amount": 90000.00, "share": 60.0 },
                { "label": "United States", "amount": 45000.00, "share": 30.0 },
                { "label": "Germany", "amount": 15000.50, "share": 10.0 }
            ]
        },
        "message": null,
        "error": null
    }
    """;

	public static string UserProfileResponse => """
    {
        "result": {
            "slug": "jean-dupont",
            "firstname": "Jean",
            "lastname": "Dupont",
            "fullname": "Jean Dupont",
            "email": "user@example.com",
            "country": "FR",
            "access_level": "plus",
            "plus_access": true,
            "pro_access": false,
            "subscription_status": "active"
        },
        "message": null,
        "error": null
    }
    """;

	public static string HoldingsAccountsResponse => """
    {
        "result": [
            {
                "id": "hold_001",
                "name": "BNP PEA",
                "balance": 45000.00
            },
            {
                "id": "hold_002",
                "name": "Boursorama CTO",
                "balance": 23000.00
            }
        ],
        "message": null,
        "error": null
    }
    """;

	public static string ApiErrorResponse(string code, string message) => $$"""
    {
        "result": null,
        "message": null,
        "error": {
            "code": "{{code}}",
            "message": "{{message}}"
        }
    }
    """;

	// --- Cookie Fixtures ---

	public static Cookie[] SessionCookies =>
	[
		new("__client", "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.test_client_jwt", "/", "clerk.finary.com")
		{
			Expires = DateTime.UtcNow.AddDays(90)
		},
		new("__client_uat", "1741776000", "/", "clerk.finary.com")
	];

	public static Cookie[] ExpiredSessionCookies =>
	[
		new("__client", "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.expired_client_jwt", "/", "clerk.finary.com")
		{
			Expires = DateTime.UtcNow.AddDays(-1)
		}
	];
}
