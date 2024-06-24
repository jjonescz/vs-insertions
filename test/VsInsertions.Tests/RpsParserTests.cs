using Meziantou.Framework.InlineSnapshotTesting;
using System.Runtime.CompilerServices;

namespace VsInsertions.Tests;

public class RpsParserTests
{
    public readonly record struct Entry(string Url, string Threads = "", string Checks = "");

    [Fact]
    public void Running()
    {
        Verify(new()
        {
            Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/558629",
            Threads = """
            {
                "pullRequestThreadContext": null,
                "id": 6552961,
                "publishedDate": "2024-06-18T04:02:14.093Z",
                "lastUpdatedDate": "2024-06-18T18:22:24.223Z",
                "comments": [
                    {
                        "id": 1,
                        "parentCommentId": 0,
                        "author": {
                            "displayName": "DevDiv Build Service (devdiv)",
                            "url": "https://spsprodwus21.vssps.visualstudio.com/00000000-0000-0000-0000-000000000000/_apis/Identities/00000000-0000-0000-0000-000000000000",
                            "_links": {
                                "avatar": {
                                    "href": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/test"
                                }
                            },
                            "id": "00000000-0000-0000-0000-000000000000",
                            "uniqueName": "",
                            "imageUrl": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/test",
                            "descriptor": "test"
                        },
                        "content": "###We've started Speedometer\r\n[Learn more about Speedometer](https://example.com)\r\n[Update 1](https://example.com)\r\n>\r\n>\r\n>:clock2: ETA 09:58 AM GMT *(around 6 hours)*\r\n>:floppy_disk: [Install your build](https://example.com)\r\n>\r\n>:rocket: [View Test Pipeline](https://example.com)\r\n### **Outages** which may impact Speedometer.\n<details><summary>There is 1 active outage:</summary>\n\n\r\n- Sev3: [PerfDDRITs and Speedometer tests run impact by Windows Updates](https://example.com)\n\r\n\n&nbsp;&nbsp;&nbsp;[View Active Outages](https://example.com)\n\r\n</details>\r\n",
                        "publishedDate": "2024-06-18T04:02:14.093Z",
                        "lastUpdatedDate": "2024-06-18T04:02:14.093Z",
                        "lastContentUpdatedDate": "2024-06-18T04:02:14.093Z",
                        "commentType": "text",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/558629/threads/6552961/comments/1"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/558629/threads/6552961"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/558629"
                            }
                        }
                    }
                ],
                "status": "fixed",
                "threadContext": null,
                "properties": {
                    "RunIdentifier": {
                        "$type": "System.String",
                        "$value": "RPS-Speedometer-14760-9743046-1"
                    },
                    "BuildId": {
                        "$type": "System.Int32",
                        "$value": 9743046
                    },
                    "DefinitionId": {
                        "$type": "System.Int32",
                        "$value": 14760
                    }
                },
                "identities": null,
                "isDeleted": false,
                "_links": {
                    "self": {
                        "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/558629/threads/6552961"
                    },
                    "repository": {
                        "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                    }
                }
            }
            """
        }, """
            Speedometer: {}
            Display:
              Short: Build: ?, DDRIT: N/A, Speedometer: ...
              Long:
                Build: Unknown
                DDRIT: Not started
                Speedometer: Running
            """);
    }

    [Fact]
    public void BrokenIterations()
    {
        Verify(new()
        {
            Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/558629",
            Threads = """
            {
                "pullRequestThreadContext": null,
                "id": 6552961,
                "publishedDate": "2024-06-18T04:02:14.093Z",
                "lastUpdatedDate": "2024-06-18T18:22:24.223Z",
                "comments": [
                    {
                        "id": 1,
                        "parentCommentId": 0,
                        "author": {
                            "displayName": "DevDiv Build Service (devdiv)",
                            "url": "https://spsprodwus21.vssps.visualstudio.com/00000000-0000-0000-0000-000000000000/_apis/Identities/00000000-0000-0000-0000-000000000000",
                            "_links": {
                                "avatar": {
                                    "href": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/test"
                                }
                            },
                            "id": "00000000-0000-0000-0000-000000000000",
                            "uniqueName": "",
                            "imageUrl": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/test",
                            "descriptor": "test"
                        },
                        "content": "###We've started Speedometer\r\n[Learn more about Speedometer](https://example.com)\r\n[Update 1](https://example.com)\r\n>\r\n>\r\n>:clock2: ETA 09:58 AM GMT *(around 6 hours)*\r\n>:floppy_disk: [Install your build](https://example.com)\r\n>\r\n>:rocket: [View Test Pipeline](https://example.com)\r\n### **Outages** which may impact Speedometer.\n<details><summary>There is 1 active outage:</summary>\n\n\r\n- Sev3: [PerfDDRITs and Speedometer tests run impact by Windows Updates](https://example.com)\n\r\n\n&nbsp;&nbsp;&nbsp;[View Active Outages](https://example.com)\n\r\n</details>\r\n",
                        "publishedDate": "2024-06-18T04:02:14.093Z",
                        "lastUpdatedDate": "2024-06-18T04:02:14.093Z",
                        "lastContentUpdatedDate": "2024-06-18T04:02:14.093Z",
                        "commentType": "text",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/558629/threads/6552961/comments/1"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/558629/threads/6552961"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/558629"
                            }
                        }
                    },
                    {
                        "id": 2,
                        "parentCommentId": 0,
                        "author": {
                            "displayName": "VSEng-PIT-Backend",
                            "url": "https://spsprodwus21.vssps.visualstudio.com/00000000-0000-0000-0000-000000000000/_apis/Identities/00000000-0000-0000-0000-000000000000",
                            "_links": {
                                "avatar": {
                                    "href": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/test"
                                }
                            },
                            "id": "00000000-0000-0000-0000-000000000000",
                            "uniqueName": "",
                            "imageUrl": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/test",
                            "descriptor": "test"
                        },
                        "content": "## :x: Test Run **FAILED**\r\n> There were 2 broken tests, please review the results below.\r\n\r\n---\r\n## 🕳 [View Performance Details on PIT](https://example.com)\r\n\r\nPR build [35017.276.dn-bot.240618.091029.558629](https://example.com)\r\nVS. *Baseline* CI build *main*-[35017.276](https://example.com)\r\n> Performance results from [Target run](https://example.com)\r\n> and [Baseline run](https://example.com)\r\n\r\n---\r\n## :no_entry: Broken tests\r\n| Test | Details | Next steps |\r\n| :---- | :---- | :---- |\r\n| ProjectSystemTest.OrchardCoreBuild.9990.Totals.CLR_AdjustedExceptions_Count_Total_devenv.Iteration:2 | Broken test<li>1 iteration was broken</li> | [🔎 View test results](https://example.com)<br /><a href=\"https://example.com\" target=\"_blank\">📂 Open test outputs</a> |\r\n| ProjectSystemTest.OrchardCoreBuild.9990.Totals.CLR_AdjustedExceptions_Count_Total_NonDevenv.Iteration:2 | Broken test<li>1 iteration was broken</li> | [🔎 View test results](https://example.com)<br /><a href=\"https://example.com\" target=\"_blank\">📂 Open test outputs</a> |\r\n\r\n\r\n<details>\r\n<summary>✅ 2 Improvements found</summary>\r\n<table>\r\n<tr><th>Found in</th><th>Details</th><th>Links</th></tr>\r\n<tr><td> FileInteractionPerfTestsAsync.TestFileOpenAndSaveLargeCSFileFromSDKProject<li>9990.Totals <ul><li><a href=\"https://example.com\" target=\"_blank\">CLR_AdjustedExceptions_Count_Total_NonDevenv</a></li></ul></li> </td><td> Improved: -9 Count (-24.43%) </td><td> <a href=\"https://example.com\" target=\"_blank\">🕳 View it in PIT</a><br /><a href=\"https://example.com\" target=\"_blank\">📂 Open test outputs</a><br /><a href=\"https://example.com\" target=\"_blank\">📈 Compare in PerfView</a> </td></tr>\r\n<tr><td> FileInteractionPerfTestsAsync.TestFileOpenAndSaveEmptyCSFile<li>9990.Totals <ul><li><a href=\"https://example.com\" target=\"_blank\">CLR_BytesAllocated_NonDevenv</a></li></ul></li> </td><td> Improved: -37,537,458 Bytes (-10.18%) </td><td> <a href=\"https://example.com\" target=\"_blank\">🕳 View it in PIT</a><br /><a href=\"https://example.com\" target=\"_blank\">📂 Open test outputs</a><br /><a href=\"https://example.com\" target=\"_blank\">📈 Compare in PerfView</a> </td></tr>\r\n</table>\r\n</details>\r\n<br />\r\n\r\n\r\n---\r\n<details> \r\n<summary>📦 Common resources...</summary>\r\n<li><a href=\"https://example.com\">Active Outages</a></li>\r\n</details> <br />\r\n\r\n<details>\r\n<summary>🤔 How to...</summary>\r\n<li> <a href=\"https://example.com\">Investigate Broken Tests Issues</a><br /> </li>\r\n<li> <a href=\"https://example.com\">Investigate Performance Regressions issues</a><br /> </li>\r\n</details> <br />\r\n\r\n\r\n",
                        "publishedDate": "2024-06-18T09:23:56.47Z",
                        "lastUpdatedDate": "2024-06-18T09:23:56.47Z",
                        "lastContentUpdatedDate": "2024-06-18T09:23:56.47Z",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/558629/threads/6552961/comments/2"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/558629/threads/6552961"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/558629"
                            }
                        }
                    }
                ],
                "status": "fixed",
                "threadContext": null,
                "properties": {
                    "RunIdentifier": {
                        "$type": "System.String",
                        "$value": "RPS-Speedometer-14760-9743046-1"
                    },
                    "BuildId": {
                        "$type": "System.Int32",
                        "$value": 9743046
                    },
                    "DefinitionId": {
                        "$type": "System.Int32",
                        "$value": 14760
                    }
                },
                "identities": null,
                "isDeleted": false,
                "_links": {
                    "self": {
                        "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/558629/threads/6552961"
                    },
                    "repository": {
                        "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                    }
                }
            }
            """
        }, """
            Speedometer:
              Finished: true
              BrokenTests: 2
            Display:
              Short: Build: ?, DDRIT: N/A, Speedometer: 0+2
              Long:
                Build: Unknown
                DDRIT: Not started
                Speedometer: Regressions: 0, Broken tests: 2
            """);
    }

    [Fact]
    public void FailedBuild()
    {
        Verify(new()
        {
            Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/558633",
            Checks = """
            {
                "configuration": {
                    "createdBy": {
                        "displayName": "John Smith",
                        "url": "https://spsprodwus21.vssps.visualstudio.com/00000000-0000-0000-0000-000000000000/_apis/Identities/00000000-0000-0000-0000-000000000000",
                        "_links": {
                            "avatar": {
                                "href": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/test"
                            }
                        },
                        "id": "00000000-0000-0000-0000-000000000000",
                        "uniqueName": "example@example.com",
                        "imageUrl": "https://example.com",
                        "descriptor": "test"
                    },
                    "createdDate": "2021-11-13T00:23:33.3008147Z",
                    "isEnabled": true,
                    "isBlocking": true,
                    "isDeleted": false,
                    "settings": {
                        "buildDefinitionId": 10310,
                        "queueOnSourceUpdateOnly": true,
                        "manualQueueOnly": false,
                        "displayName": "CloudBuild - PR",
                        "validDuration": 4320,
                        "scope": [
                            {
                                "refName": "refs/heads/main",
                                "matchKind": "Exact",
                                "repositoryId": "a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            }
                        ]
                    },
                    "isEnterpriseManaged": false,
                    "_links": {
                        "self": {
                            "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/configurations/8171"
                        },
                        "policyType": {
                            "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/types/0609b952-1397-4640-95ec-e00a01b2c241"
                        }
                    },
                    "revision": 5,
                    "id": 8171,
                    "url": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/configurations/8171",
                    "type": {
                        "id": "0609b952-1397-4640-95ec-e00a01b2c241",
                        "url": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/types/0609b952-1397-4640-95ec-e00a01b2c241",
                        "displayName": "Build"
                    }
                },
                "artifactId": "vstfs:///CodeReview/CodeReviewId/0bdbc590-a062-4c3f-b0f6-9383f67865ee%2f558633",
                "evaluationId": "284f5e50-37a6-4138-857f-1b9a43c5fb8c",
                "startedDate": "2024-06-18T03:13:23.9185139Z",
                "completedDate": "2024-06-18T03:41:23.2075138Z",
                "status": "rejected",
                "context": {
                    "lastMergeCommitId": "08d20174821c24fd6e0135290d76a9eac2cf376f",
                    "lastMergeSourceCommitId": "a2114d9b910eac4c585dd2b16f59607baff74178",
                    "lastMergeTargetCommitId": "ff59e9e0f35f2468d42fcdc177119906add38e50",
                    "buildId": 9742932,
                    "buildDefinitionId": 10310,
                    "buildDefinitionName": "DD-CB-PR",
                    "buildIsNotCurrent": false,
                    "buildStartedUtc": "2024-06-18T03:13:15.5036803Z",
                    "isExpired": false,
                    "buildAfterMerge": false,
                    "wasAutoRequeued": false,
                    "buildOutputPreview": null
                }
            }
            """,
        }, """
            BuildStatus:
              Status: Rejected
            Display:
              Short: Build: ✘, DDRIT: N/A, Speedometer: N/A
              Long:
                Build: Rejected
                DDRIT: Not started
                Speedometer: Not started
            """);
    }

    [Fact]
    public void ExpiredBuild()
    {
        Verify(new()
        {
            Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/559601",
            Checks = """
            {
                "configuration": {
                    "createdBy": {
                        "displayName": "John Smith",
                        "url": "https://spsprodwus21.vssps.visualstudio.com/00000000-0000-0000-0000-000000000000/_apis/Identities/00000000-0000-0000-0000-000000000000",
                        "_links": {
                            "avatar": {
                                "href": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/test"
                            }
                        },
                        "id": "00000000-0000-0000-0000-000000000000",
                        "uniqueName": "example@example.com",
                        "imageUrl": "https://example.com",
                        "descriptor": "test"
                    },
                    "createdDate": "2021-11-13T00:23:33.3008147Z",
                    "isEnabled": true,
                    "isBlocking": true,
                    "isDeleted": false,
                    "settings": {
                        "buildDefinitionId": 10310,
                        "queueOnSourceUpdateOnly": true,
                        "manualQueueOnly": false,
                        "displayName": "CloudBuild - PR",
                        "validDuration": 4320,
                        "scope": [
                            {
                                "refName": "refs/heads/main",
                                "matchKind": "Exact",
                                "repositoryId": "a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            }
                        ]
                    },
                    "isEnterpriseManaged": false,
                    "_links": {
                        "self": {
                            "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/configurations/8171"
                        },
                        "policyType": {
                            "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/types/0609b952-1397-4640-95ec-e00a01b2c241"
                        }
                    },
                    "revision": 5,
                    "id": 8171,
                    "url": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/configurations/8171",
                    "type": {
                        "id": "0609b952-1397-4640-95ec-e00a01b2c241",
                        "url": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/types/0609b952-1397-4640-95ec-e00a01b2c241",
                        "displayName": "Build"
                    }
                },
                "artifactId": "vstfs:///CodeReview/CodeReviewId/0bdbc590-a062-4c3f-b0f6-9383f67865ee%2f559601",
                "evaluationId": "0c1ce0b7-34df-4350-874e-67646ce20e9d",
                "startedDate": "2024-06-21T01:08:55.9196247Z",
                "completedDate": "2024-06-21T02:40:53.4452644Z",
                "status": "queued",
                "context": {
                    "lastMergeCommitId": "a973c6ec21949fe84825102d115b9bddf9cf1053",
                    "lastMergeSourceCommitId": "007e18e77db4c46b879f043b136aa1601f70d212",
                    "lastMergeTargetCommitId": "f753fb8783b4c07ce1a79a9d3c90b44c0c86c1ba",
                    "buildId": 9763487,
                    "buildDefinitionId": 10310,
                    "buildDefinitionName": "DD-CB-PR",
                    "buildIsNotCurrent": true,
                    "buildStartedUtc": "2024-06-21T01:08:47.3364673Z",
                    "isExpired": true,
                    "buildAfterMerge": false,
                    "wasAutoRequeued": false,
                    "buildOutputPreview": null
                }
            }
            """,
        }, """
            BuildStatus:
              Status: Queued
              IsExpired: true
            Display:
              Short: Build: E, DDRIT: N/A, Speedometer: N/A
              Long:
                Build: Expired (Queued)
                DDRIT: Not started
                Speedometer: Not started
            """);
    }

    [InlineSnapshotAssertion(parameterName: nameof(expected))]
    private static void Verify(Entry input, string? expected = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = -1)
    {
        var parser = new RpsParser();
        var rpsSummary = new RpsSummary();
        parser.ParseRpsSummary(
            threadsJson: wrapJson(input.Threads),
            checksJson: wrapJson(input.Checks),
            rpsSummary);
        InlineSnapshot.Validate(rpsSummary, expected, filePath, lineNumber);

        static string wrapJson(string value) => $$"""
            {
                "value": [{{value}}]
            }
            """;
    }
}
