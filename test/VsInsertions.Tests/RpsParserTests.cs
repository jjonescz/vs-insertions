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
              BrokenTests: 2
              Flags: Finished
            Display:
              Short: Build: ?, DDRIT: N/A, Speedometer: 0+2
              Long:
                Build: Unknown
                DDRIT: Not started
                Speedometer: Regressions: 0, Broken tests: 2
            """);
    }

    [Fact]
    public void MissingBaseline()
    {
        Verify(new()
        {
            Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/559879",
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
                        "publishedDate": "2024-06-22T04:49:05.063Z",
                        "lastUpdatedDate": "2024-06-22T04:49:05.063Z",
                        "lastContentUpdatedDate": "2024-06-22T04:49:05.063Z",
                        "commentType": "text",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/559879/threads/6569782/comments/1"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/559879/threads/6569782"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/559879"
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
                        "content": "## :x: Test run **FAILED**\r\n> There was no baseline run to compare against.\r\n\r\n---\r\n## 🕳 [View Performance Details in PIT](https://example.com)\r\n\r\n---\r\n:x: No baseline found for comparison. Missing RPS regression coverage. \r\n[More Info](https://example.com)\r\n*Target* PR build [35021.68.dn-bot.240622.091357.559879](https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=9769812)\r\n> Performance results from [Target run](https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=9770008)\r\n\r\n---\r\n<details> \r\n<summary>📦 Common resources...</summary>\r\n<li><a href=\"https://example.com\">Active Outages</a></li>\r\n</details> <br />\r\n\r\n\r\n",
                        "publishedDate": "2024-06-22T09:50:25.653Z",
                        "lastUpdatedDate": "2024-06-22T09:50:25.653Z",
                        "lastContentUpdatedDate": "2024-06-22T09:50:25.653Z",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/559879/threads/6569782/comments/2"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/559879/threads/6569782"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/559879"
                            }
                        }
                    }
                ],
                "status": "fixed",
                "threadContext": null,
                "properties": {
                    "RunIdentifier": {
                        "$type": "System.String",
                        "$value": "RPS-Speedometer-14760-9770008-1"
                    },
                    "BuildId": {
                        "$type": "System.Int32",
                        "$value": 9770008
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
                        "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/559879/threads/6569782"
                    },
                    "repository": {
                        "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                    }
                }
            }
            """
        }, """
            Speedometer:
              Regressions: -1
              BrokenTests: -1
              Flags: Finished, MissingBaseline
            Display:
              Short: Build: ?, DDRIT: N/A, Speedometer: B
              Long:
                Build: Unknown
                DDRIT: Not started
                Speedometer: Missing baseline
            """);
    }

    [Fact]
    public void InfraIssue()
    {
        Verify(new()
        {
            Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/559881",
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
                        "publishedDate": "2024-06-22T04:43:01.737Z",
                        "lastUpdatedDate": "2024-06-22T04:43:01.737Z",
                        "lastContentUpdatedDate": "2024-06-22T04:43:01.737Z",
                        "commentType": "text",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/559881/threads/6569778/comments/1"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/559881/threads/6569778"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/559881"
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
                        "content": "## :x: Test run **FAILED**\r\n> There was an infrastructure issue in this run.\r\n\r\n---\r\n## :heavy_exclamation_mark: An analysis could not be provided\r\nThere was an **infrastructure issue** while ** on this run\r\n\r\n\r\n---\r\n<details> \r\n<summary>📦 Common resources...</summary>\r\n<li><a href=\"https://example.com\">Active Outages</a></li>\r\n</details> <br />\r\n\r\n\r\n",
                        "publishedDate": "2024-06-22T07:51:09.48Z",
                        "lastUpdatedDate": "2024-06-22T07:51:09.48Z",
                        "lastContentUpdatedDate": "2024-06-22T07:51:09.48Z",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/559881/threads/6569778/comments/2"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/559881/threads/6569778"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/559881"
                            }
                        }
                    }
                ],
                "status": "active",
                "threadContext": null,
                "properties": {
                    "RunIdentifier": {
                        "$type": "System.String",
                        "$value": "RPS-Speedometer-14760-9770000-1"
                    },
                    "BuildId": {
                        "$type": "System.Int32",
                        "$value": 9770000
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
                        "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/559881/threads/6569778"
                    },
                    "repository": {
                        "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                    }
                }
            }
            """
        }, """
            Speedometer:
              Regressions: -1
              BrokenTests: -1
              Flags: Finished, InfraIssue
            Display:
              Short: Build: ?, DDRIT: N/A, Speedometer: I
              Long:
                Build: Unknown
                DDRIT: Not started
                Speedometer: Infrastructure issue
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
              Expires: 2024-06-21T03:13:15.5036803+00:00
            Display:
              Short: Build: ✘, DDRIT: N/A, Speedometer: N/A
              Long:
                Build: Rejected (expires 2024-06-21T03:13:15.5036803+00:00)
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
              Expires: 2024-06-24T01:08:47.3364673+00:00
            Display:
              Short: Build: E, DDRIT: N/A, Speedometer: N/A
              Long:
                Build: Expired (Queued)
                DDRIT: Not started
                Speedometer: Not started
            """);
    }

    [Fact]
    public void ScopedSpeedometer()
    {
        Verify(new()
        {
            Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/594020",
            Threads = """
            {
                "pullRequestThreadContext": null,
                "id": 7103615,
                "publishedDate": "2024-11-21T03:18:36.473Z",
                "lastUpdatedDate": "2024-11-21T09:45:31.687Z",
                "comments": [
                    {
                        "id": 1,
                        "parentCommentId": 0,
                        "author": {
                            "displayName": "DevDiv Build Service (devdiv)",
                            "url": "https://example.com",
                            "_links": {
                                "avatar": {
                                    "href": "https://example.com"
                                }
                            },
                            "id": "6d3b3c1a-123d-454c-a78b-b4a426164711",
                            "uniqueName": "",
                            "imageUrl": "https://example.com"
                        },
                        "content": "###We've started Speedometer\r\n[Learn more about Speedometer](https://example.com)\r\n[Update 1](https://dev.azure.com/DevDiv/DevDiv/_git/VS/pullrequest/594020?_a=files&iteration=1&base=0)\r\n>\r\n>\r\n>:clock2: ETA 02:23 PM GMT *(around 11 hours)*\r\n>:floppy_disk: [Install your build](https://example.com)\r\n>\r\n>:rocket: [View Test Pipeline](https://example.com)\r\n### There are no active outages which may impact Speedometer\n\r\n\n&nbsp;&nbsp;&nbsp;[View Active Outages](https://example.com)\n\r\n",
                        "publishedDate": "2024-11-21T03:18:36.473Z",
                        "lastUpdatedDate": "2024-11-21T03:18:36.473Z",
                        "lastContentUpdatedDate": "2024-11-21T03:18:36.473Z",
                        "commentType": "text",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/594020/threads/7103615/comments/1"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/594020/threads/7103615"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/594020"
                            }
                        }
                    },
                    {
                        "id": 2,
                        "parentCommentId": 0,
                        "author": {
                            "displayName": "VSEng-PIT-Backend",
                            "url": "https://example.com",
                            "_links": {
                                "avatar": {
                                    "href": "https://example.com"
                                }
                            },
                            "id": "c7965a43-7b7a-6e71-a8d0-274bdbcbc36a",
                            "uniqueName": "",
                            "imageUrl": "https://example.com"
                        },
                        "content": "## :x: Test Run **FAILED**\r\n> There was 1 regression, please review the results below.\r\n<li><a href=\"https://example.com\">Known Noise and Broken Test Issues</a></li>\r\n\r\n---\r\n## 🕳 [View Performance Details on PIT](https://example.com)\r\n\r\nPR build [35520.184.dn-bot.241121.092831.594020](https://example.com)\r\nVS. *Baseline* CI build *main*-[35520.184](https://example.com)\r\n> Performance results from [Target run](https://example.com)\r\n> and [Baseline run](https://example.com)\r\n\r\n---\r\n## :triangular_flag_on_post: Regressions\r\n\r\n| Found in | Details | Next steps |\r\n| :---- | :---- | :---- |\r\n| CPlusPlusWithCache.UnrealEngine52<li>0850.Change Solution Configuration - Warm <ul><li><a href=\"https://example.com\" target=\"_blank\">Duration_TotalElapsedTime</a></li></ul></li> | Regressed: 1,241 ms (26.87%) | [🕳 View it in PIT](https://example.com)<br />[:open_file_folder: Open test outputs](https://example.com)<br />[:chart_with_upwards_trend: Compare in PerfView](https://example.com) |\r\n\r\n\r\n\r\n---\r\n<details> \r\n<summary>🤔 How to...</summary>\r\n<li> <a href=\"https://example.com\">Investigate Broken Tests Issues</a><br /> </li>\r\n<li> <a href=\"https://example.com\">Investigate Performance Regressions issues</a><br /> </li>\r\n</details> <br />\r\n\r\n\r\n",
                        "publishedDate": "2024-11-21T09:45:31.687Z",
                        "lastUpdatedDate": "2024-11-21T09:45:31.687Z",
                        "lastContentUpdatedDate": "2024-11-21T09:45:31.687Z",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/594020/threads/7103615/comments/2"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/594020/threads/7103615"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/594020"
                            }
                        }
                    }
                ],
                "status": "active",
                "threadContext": null,
                "properties": {
                    "RunIdentifier": {
                        "$type": "System.String",
                        "$value": "RPS-Speedometer-14760-10593388-1"
                    },
                    "BuildId": {
                        "$type": "System.Int32",
                        "$value": 10593388
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
                        "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/594020/threads/7103615"
                    },
                    "repository": {
                        "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                    }
                }
            },
            {
                "pullRequestThreadContext": null,
                "id": 7103622,
                "publishedDate": "2024-11-21T03:21:01.92Z",
                "lastUpdatedDate": "2024-11-21T07:50:20.23Z",
                "comments": [
                    {
                        "id": 1,
                        "parentCommentId": 0,
                        "author": {
                            "displayName": "DevDiv Build Service (devdiv)",
                            "url": "https://example.com",
                            "_links": {
                                "avatar": {
                                    "href": "https://example.com"
                                }
                            },
                            "id": "6d3b3c1a-123d-454c-a78b-b4a426164711",
                            "uniqueName": "",
                            "imageUrl": "https://example.com"
                        },
                        "content": "###We've started Speedometer-Scoped for Roslyn Insertion\r\n[Update 1](https://dev.azure.com/DevDiv/DevDiv/_git/VS/pullrequest/594020?_a=files&iteration=1&base=0)\r\n>\r\n>\r\n>:clock2: ETA 08:58 AM GMT *(around 6 hours)*\r\n>:floppy_disk: [Install your build](https://example.com)\r\n>\r\n>:rocket: [View Test Pipeline](https://example.com)\r\n### There are no active outages which may impact Speedometer-Scoped for Roslyn Insertion\n\r\n\n&nbsp;&nbsp;&nbsp;[View Active Outages](https://example.com)\n\r\n",
                        "publishedDate": "2024-11-21T03:21:01.92Z",
                        "lastUpdatedDate": "2024-11-21T03:21:01.92Z",
                        "lastContentUpdatedDate": "2024-11-21T03:21:01.92Z",
                        "commentType": "text",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/594020/threads/7103622/comments/1"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/594020/threads/7103622"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/594020"
                            }
                        }
                    },
                    {
                        "id": 2,
                        "parentCommentId": 0,
                        "author": {
                            "displayName": "VSEng-PIT-Backend",
                            "url": "https://example.com",
                            "_links": {
                                "avatar": {
                                    "href": "https://example.com"
                                }
                            },
                            "id": "c7965a43-7b7a-6e71-a8d0-274bdbcbc36a",
                            "uniqueName": "",
                            "imageUrl": "https://example.com"
                        },
                        "content": "## :heavy_check_mark: Test Run **PASSED**\r\n> > There were no test failures or performance regressions\r\n\r\n---\r\n## 🕳 [View Performance Details on PIT](https://example.com)\r\n\r\nPR build [35520.184.dn-bot.241121.071622.594020](https://example.com)\r\nVS. *Baseline* CI build *main*-[35520.160](https://example.com)\r\n> Performance results from [Target run](https://example.com)\r\n> and [Baseline run](https://example.com)\r\n\r\n:warning: Using a 'Last Known Good' baseline run.\r\n> For more information about this [click here](https://example.com)\r\n\r\n---\r\n\r\n---\r\n<details> \r\n<summary>🤔 How to...</summary>\r\n<li> <a href=\"https://example.com\">Investigate Broken Tests Issues</a><br /> </li>\r\n<li> <a href=\"https://example.com\">Investigate Performance Regressions issues</a><br /> </li>\r\n</details> <br />\r\n\r\n\r\n",
                        "publishedDate": "2024-11-21T07:49:21.837Z",
                        "lastUpdatedDate": "2024-11-21T07:49:21.837Z",
                        "lastContentUpdatedDate": "2024-11-21T07:49:21.837Z",
                        "usersLiked": [],
                        "_links": {
                            "self": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/594020/threads/7103622/comments/2"
                            },
                            "repository": {
                                "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                            },
                            "threads": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/594020/threads/7103622"
                            },
                            "pullRequests": {
                                "href": "https://dev.azure.com/devdiv/_apis/git/pullRequests/594020"
                            }
                        }
                    }
                ],
                "status": "closed",
                "threadContext": null,
                "properties": {
                    "RunIdentifier": {
                        "$type": "System.String",
                        "$value": "RPS-Speedometer-21995-10593411-1"
                    },
                    "BuildId": {
                        "$type": "System.Int32",
                        "$value": 10593411
                    },
                    "DefinitionId": {
                        "$type": "System.Int32",
                        "$value": 21995
                    }
                },
                "identities": null,
                "isDeleted": false,
                "_links": {
                    "self": {
                        "href": "https://dev.azure.com/devdiv/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d/pullRequests/594020/threads/7103622"
                    },
                    "repository": {
                        "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/git/repositories/a290117c-5a8a-40f7-bc2c-f14dbe3acf6d"
                    }
                }
            }
            """,
        }, """
            SpeedometerScoped:
              Flags: Finished
            Speedometer:
              Regressions: 1
              BrokenTests: -1
              Flags: Finished
            Display:
              Short: Build: ?, DDRIT: N/A, Speedometer-Scoped: 0, Speedometer: 1
              Long:
                Build: Unknown
                DDRIT: Not started
                Speedometer-Scoped: Regressions: 0
                Speedometer: Regressions: 1
            """);
    }

    [Fact]
    public void FailedDesktopValidation()
    {
        Verify(new()
        {
            Url = "https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/594268",
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
                    "createdDate": "2024-08-22T21:44:05.4060185Z",
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
                    "revision": 10,
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
                "startedDate": "2024-11-21T22:25:45.5374566Z",
                "completedDate": "2024-11-22T00:06:28.502424Z",
                "status": "approved",
                "context": {
                    "lastMergeCommitId": "68d748cd99082dbbd29e9c6da1dab0a40270c76a",
                    "lastMergeSourceCommitId": "bce87b090700562f3cf5ad265b18deb91c3169bc",
                    "lastMergeTargetCommitId": "3fbf085219e85894be35e057a82f543cb08d17c0",
                    "buildId": 10598285,
                    "buildDefinitionId": 10310,
                    "buildDefinitionName": "DD-CB-PR",
                    "buildIsNotCurrent": true,
                    "buildStartedUtc": "2024-11-21T22:25:35.8450886Z",
                    "isExpired": false,
                    "buildAfterMerge": false,
                    "wasAutoRequeued": false,
                    "buildOutputPreview": null
                }
            },
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
                    "createdDate": "2024-08-22T21:44:13.3435714Z",
                    "isEnabled": true,
                    "isBlocking": true,
                    "isDeleted": false,
                    "settings": {
                        "buildDefinitionId": 18443,
                        "queueOnSourceUpdateOnly": true,
                        "manualQueueOnly": false,
                        "displayName": "Desktop Validation - MSBuild Retail ",
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
                            "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/configurations/20247"
                        },
                        "policyType": {
                            "href": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/types/0609b952-1397-4640-95ec-e00a01b2c241"
                        }
                    },
                    "revision": 19,
                    "id": 20247,
                    "url": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/configurations/20247",
                    "type": {
                        "id": "0609b952-1397-4640-95ec-e00a01b2c241",
                        "url": "https://dev.azure.com/devdiv/0bdbc590-a062-4c3f-b0f6-9383f67865ee/_apis/policy/types/0609b952-1397-4640-95ec-e00a01b2c241",
                        "displayName": "Build"
                    }
                },
                "artifactId": "vstfs:///CodeReview/CodeReviewId/0bdbc590-a062-4c3f-b0f6-9383f67865ee%2f594268",
                "evaluationId": "0dce4aef-6ac8-44ce-a72f-758a5719cd33",
                "startedDate": "2024-11-21T22:25:45.5374566Z",
                "completedDate": "2024-11-21T23:44:23.9392948Z",
                "status": "rejected",
                "context": {
                    "lastMergeCommitId": "68d748cd99082dbbd29e9c6da1dab0a40270c76a",
                    "lastMergeSourceCommitId": "bce87b090700562f3cf5ad265b18deb91c3169bc",
                    "lastMergeTargetCommitId": "3fbf085219e85894be35e057a82f543cb08d17c0",
                    "buildId": 10598293,
                    "buildDefinitionId": 18443,
                    "buildDefinitionName": "MSBuild Retail Validation - PR",
                    "buildIsNotCurrent": false,
                    "buildStartedUtc": "2024-11-21T22:25:44.7207165Z",
                    "isExpired": false,
                    "buildAfterMerge": false,
                    "wasAutoRequeued": false,
                    "buildOutputPreview": {
                        "jobName": "MSBuild Retail Validation",
                        "taskName": "Run MSBuild",
                        "errors": [
                            {
                                "lineNumber": 16677,
                                "message": "C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\MSBuild\\Microsoft\\VisualStudio\\v17.0\\AppxPackage\\Microsoft.AppXPackage.Targets(1468,5): Error MSB3816: Loading assembly \"C:\\Users\\cloudtest\\.nuget\\packages\\runtime.win10-x86.microsoft.netcore.universalwindowsplatform\\6.2.14\\runtimes\\win10-x86\\lib\\uap10.0.15138\\Microsoft.Win32.Primitives.dll\" failed. System.IO.FileNotFoundException: Could not load file or assembly 'System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7ce..."
                            },
                            {
                                "lineNumber": 16715,
                                "message": "C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\MSBuild\\Microsoft\\VisualStudio\\v17.0\\AppxPackage\\Microsoft.AppXPackage.Targets(1468,5): Error MSB3816: Loading assembly \"C:\\Users\\cloudtest\\.nuget\\packages\\runtime.win10-x86.microsoft.netcore.universalwindowsplatform\\6.2.14\\runtimes\\win10-x86\\lib\\uap10.0.15138\\System.AppContext.dll\" failed. System.IO.FileNotFoundException: Could not load file or assembly 'System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7..."
                            },
                            {
                                "lineNumber": 16753,
                                "message": "C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\MSBuild\\Microsoft\\VisualStudio\\v17.0\\AppxPackage\\Microsoft.AppXPackage.Targets(1468,5): Error MSB3816: Loading assembly \"C:\\Users\\cloudtest\\.nuget\\packages\\runtime.win10-x86.microsoft.netcore.universalwindowsplatform\\6.2.14\\runtimes\\win10-x86\\lib\\uap10.0.15138\\System.Buffers.dll\" failed. System.IO.FileNotFoundException: Could not load file or assembly 'System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798..."
                            }
                        ]
                    }
                }
            }
            """,
        }, """
            BuildStatus:
              Status: Approved
              Expires: 2024-11-24T22:25:35.8450886+00:00
            DesktopValidationStatus:
              Status: Rejected
              Expires: 2024-11-24T22:25:44.7207165+00:00
              OutputPreview:
                C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\VisualStudio\v17.0\AppxPackage\Microsoft.AppXPackage.Targets(1468,5): Error MSB3816: Loading assembly "C:\Users\cloudtest\.nuget\packages\runtime.win10-x86.microsoft.netcore.universalwindowsplatform\6.2.14\runtimes\win10-x86\lib\uap10.0.15138\Microsoft.Win32.Primitives.dll" failed. System.IO.FileNotFoundException: Could not load file or assembly 'System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7ce...
                C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\VisualStudio\v17.0\AppxPackage\Microsoft.AppXPackage.Targets(1468,5): Error MSB3816: Loading assembly "C:\Users\cloudtest\.nuget\packages\runtime.win10-x86.microsoft.netcore.universalwindowsplatform\6.2.14\runtimes\win10-x86\lib\uap10.0.15138\System.AppContext.dll" failed. System.IO.FileNotFoundException: Could not load file or assembly 'System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7...
                C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\VisualStudio\v17.0\AppxPackage\Microsoft.AppXPackage.Targets(1468,5): Error MSB3816: Loading assembly "C:\Users\cloudtest\.nuget\packages\runtime.win10-x86.microsoft.netcore.universalwindowsplatform\6.2.14\runtimes\win10-x86\lib\uap10.0.15138\System.Buffers.dll" failed. System.IO.FileNotFoundException: Could not load file or assembly 'System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798...
            Display:
              Short: Build: ✔, DesktopValidation: ✘, DDRIT: N/A, Speedometer: N/A
              Long:
                Build: Approved (expires 2024-11-24T22:25:35.8450886+00:00)
                DesktopValidation: Rejected (expires 2024-11-24T22:25:44.7207165+00:00)

                Output preview:
                C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\VisualStudio\v17.0\AppxPackage\Microsoft.AppXPackage.Targets(1468,5): Error MSB3816: Loading assembly "C:\Users\cloudtest\.nuget\packages\runtime.win10-x86.microsoft.netcore.universalwindowsplatform\6.2.14\runtimes\win10-x86\lib\uap10.0.15138\Microsoft.Win32.Primitives.dll" failed. System.IO.FileNotFoundException: Could not load file or assembly 'System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7ce...
                C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\VisualStudio\v17.0\AppxPackage\Microsoft.AppXPackage.Targets(1468,5): Error MSB3816: Loading assembly "C:\Users\cloudtest\.nuget\packages\runtime.win10-x86.microsoft.netcore.universalwindowsplatform\6.2.14\runtimes\win10-x86\lib\uap10.0.15138\System.AppContext.dll" failed. System.IO.FileNotFoundException: Could not load file or assembly 'System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7...
                C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\VisualStudio\v17.0\AppxPackage\Microsoft.AppXPackage.Targets(1468,5): Error MSB3816: Loading assembly "C:\Users\cloudtest\.nuget\packages\runtime.win10-x86.microsoft.netcore.universalwindowsplatform\6.2.14\runtimes\win10-x86\lib\uap10.0.15138\System.Buffers.dll" failed. System.IO.FileNotFoundException: Could not load file or assembly 'System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798...
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
