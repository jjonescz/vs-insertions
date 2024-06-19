using Meziantou.Framework.InlineSnapshotTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VsInsertions.Tests;

public class RpsParserTests
{
    public readonly record struct Entry(string Url, string Threads);

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
                            "url": "https://spsprodwus21.vssps.visualstudio.com/A0ba55e64-8cdb-444e-beea-1056cf958523/_apis/Identities/6d3b3c1a-123d-454c-a78b-b4a426164711",
                            "_links": {
                                "avatar": {
                                    "href": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/svc.MGJhNTVlNjQtOGNkYi00NDRlLWJlZWEtMTA1NmNmOTU4NTIzOkJ1aWxkOjBiZGJjNTkwLWEwNjItNGMzZi1iMGY2LTkzODNmNjc4NjVlZQ"
                                }
                            },
                            "id": "6d3b3c1a-123d-454c-a78b-b4a426164711",
                            "uniqueName": "",
                            "imageUrl": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/svc.MGJhNTVlNjQtOGNkYi00NDRlLWJlZWEtMTA1NmNmOTU4NTIzOkJ1aWxkOjBiZGJjNTkwLWEwNjItNGMzZi1iMGY2LTkzODNmNjc4NjVlZQ",
                            "descriptor": "svc.MGJhNTVlNjQtOGNkYi00NDRlLWJlZWEtMTA1NmNmOTU4NTIzOkJ1aWxkOjBiZGJjNTkwLWEwNjItNGMzZi1iMGY2LTkzODNmNjc4NjVlZQ"
                        },
                        "content": "###We've started Speedometer\r\n[Learn more about Speedometer](https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki/24357/Speedometer)\r\n[Update 1](https://dev.azure.com/DevDiv/DevDiv/_git/VS/pullrequest/558629?_a=files&iteration=1&base=0)\r\n>\r\n>\r\n>:clock2: ETA 09:58 AM GMT *(around 6 hours)*\r\n>:floppy_disk: [Install your build](https://vsdrop.corp.microsoft.com/file/v1/Products/DevDiv/VS/a792c33b35dfffc5bd2d648861ffdb4b4ba34534/2c80eaba-6f1d-a165-850b-d0d16a8d7085;bootstrappers/Enterprise/vs_enterprise.exe)\r\n>\r\n>:rocket: [View Test Pipeline](https://devdiv.visualstudio.com/DevDiv/_build/results?buildId=9743046)\r\n### **Outages** which may impact Speedometer.\n<details><summary>There is 1 active outage:</summary>\n\n\r\n- Sev3: [PerfDDRITs and Speedometer tests run impact by Windows Updates](https://portal.microsofticm.com/imp/v3/outages/details/504334001/home)\n\r\n\n&nbsp;&nbsp;&nbsp;[View Active Outages](https://portal.microsofticm.com/imp/v3/outages/dashboard/vsengineering/declaredoutages)\n\r\n</details>\r\n",
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
                            "url": "https://spsprodwus21.vssps.visualstudio.com/A0ba55e64-8cdb-444e-beea-1056cf958523/_apis/Identities/c7965a43-7b7a-6e71-a8d0-274bdbcbc36a",
                            "_links": {
                                "avatar": {
                                    "href": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/aadsp.Yzc5NjVhNDMtN2I3YS03ZTcxLWE4ZDAtMjc0YmRiY2JjMzZh"
                                }
                            },
                            "id": "c7965a43-7b7a-6e71-a8d0-274bdbcbc36a",
                            "uniqueName": "",
                            "imageUrl": "https://dev.azure.com/devdiv/_apis/GraphProfile/MemberAvatars/aadsp.Yzc5NjVhNDMtN2I3YS03ZTcxLWE4ZDAtMjc0YmRiY2JjMzZh",
                            "descriptor": "aadsp.Yzc5NjVhNDMtN2I3YS03ZTcxLWE4ZDAtMjc0YmRiY2JjMzZh"
                        },
                        "content": "## :x: Test Run **FAILED**\r\n> There were 2 broken tests, please review the results below.\r\n\r\n---\r\n## 🕳 [View Performance Details on PIT](https://aka.ms/vsengpit?targetBuild=35017.276.dn-bot.240618.091029.558629&targetBranch=main&targetPerfBuildId=9743046&runGroup=Speedometer&since=2024-06-18&baselineBuild=35017.276&baselineBranch=main)\r\n\r\nPR build [35017.276.dn-bot.240618.091029.558629](https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=9742807)\r\nVS. *Baseline* CI build *main*-[35017.276](https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=9742673)\r\n> Performance results from [Target run](https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=9743046)\r\n> and [Baseline run](https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=9742981)\r\n\r\n---\r\n## :no_entry: Broken tests\r\n| Test | Details | Next steps |\r\n| :---- | :---- | :---- |\r\n| ProjectSystemTest.OrchardCoreBuild.9990.Totals.CLR_AdjustedExceptions_Count_Total_devenv.Iteration:2 | Broken test<li>1 iteration was broken</li> | [🔎 View test results](https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=9743046&view=ms.vss-test-web.build-test-results-tab)<br /><a href=\"https://aka.ms/VsEngDropExplorer?dropName=Logs/DevDiv/VS/a792c33b35dfffc5bd2d648861ffdb4b4ba34534/9743046/14760/1&windowTitle=35017.276.dn-bot.240618.091029.558629&path=TestExecutionOutputs/ProjectSystemTest.OrchardCoreBuild\" target=\"_blank\">📂 Open test outputs</a> |\r\n| ProjectSystemTest.OrchardCoreBuild.9990.Totals.CLR_AdjustedExceptions_Count_Total_NonDevenv.Iteration:2 | Broken test<li>1 iteration was broken</li> | [🔎 View test results](https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=9743046&view=ms.vss-test-web.build-test-results-tab)<br /><a href=\"https://aka.ms/VsEngDropExplorer?dropName=Logs/DevDiv/VS/a792c33b35dfffc5bd2d648861ffdb4b4ba34534/9743046/14760/1&windowTitle=35017.276.dn-bot.240618.091029.558629&path=TestExecutionOutputs/ProjectSystemTest.OrchardCoreBuild\" target=\"_blank\">📂 Open test outputs</a> |\r\n\r\n\r\n<details>\r\n<summary>✅ 2 Improvements found</summary>\r\n<table>\r\n<tr><th>Found in</th><th>Details</th><th>Links</th></tr>\r\n<tr><td> FileInteractionPerfTestsAsync.TestFileOpenAndSaveLargeCSFileFromSDKProject<li>9990.Totals <ul><li><a href=\"https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki?pagePath=/Engineering-System-%26-Tools/Test-Docs/RPS/RPS-User-Guide/Investigations-and-Walkthroughs/CLR_Exceptions_Count\" target=\"_blank\">CLR_AdjustedExceptions_Count_Total_NonDevenv</a></li></ul></li> </td><td> Improved: -9 Count (-24.43%) </td><td> <a href=\"https://aka.ms/vsengpit?targetBuild=35017.276.dn-bot.240618.091029.558629&amp;targetBranch=main&amp;targetPerfBuildId=9743046&amp;runGroup=Speedometer&amp;since=2024-06-18&amp;baselineBuild=35017.276&amp;baselineBranch=main&amp;selectedCounters=FileInteractionPerfTestsAsync.TestFileOpenAndSaveLargeCSFileFromSDKProject>9990.Totals>CLR_AdjustedExceptions_Count_Total_NonDevenv\" target=\"_blank\">🕳 View it in PIT</a><br /><a href=\"https://aka.ms/VsEngDropExplorer?dropName=Logs/DevDiv/VS/a792c33b35dfffc5bd2d648861ffdb4b4ba34534/9743046/14760/1&amp;windowTitle=35017.276.dn-bot.240618.091029.558629&amp;path=TestExecutionOutputs/FileInteractionPerfTestsAsync.TestFileOpenAndSaveLargeCSFileFromSDKProject\" target=\"_blank\">📂 Open test outputs</a><br /><a href=\"https://aka.ms/VsEngDropExplorer?compareDrops=on&amp;currentDropName=Logs/DevDiv/VS/a792c33b35dfffc5bd2d648861ffdb4b4ba34534/9743046/14760/1&amp;baselineDropName=Logs/DevDiv/VS/ff59e9e0f35f2468d42fcdc177119906add38e50/9742981/14801/1&amp;folderName=FileInteractionPerfTestsAsync.TestFileOpenAndSaveLargeCSFileFromSDKProject\" target=\"_blank\">📈 Compare in PerfView</a> </td></tr>\r\n<tr><td> FileInteractionPerfTestsAsync.TestFileOpenAndSaveEmptyCSFile<li>9990.Totals <ul><li><a href=\"https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki?pagePath=/Engineering-System-%26-Tools/Test-Docs/RPS/RPS-User-Guide/Investigations-and-Walkthroughs/CLR_BytesAllocated\" target=\"_blank\">CLR_BytesAllocated_NonDevenv</a></li></ul></li> </td><td> Improved: -37,537,458 Bytes (-10.18%) </td><td> <a href=\"https://aka.ms/vsengpit?targetBuild=35017.276.dn-bot.240618.091029.558629&amp;targetBranch=main&amp;targetPerfBuildId=9743046&amp;runGroup=Speedometer&amp;since=2024-06-18&amp;baselineBuild=35017.276&amp;baselineBranch=main&amp;selectedCounters=FileInteractionPerfTestsAsync.TestFileOpenAndSaveEmptyCSFile>9990.Totals>CLR_BytesAllocated_NonDevenv\" target=\"_blank\">🕳 View it in PIT</a><br /><a href=\"https://aka.ms/VsEngDropExplorer?dropName=Logs/DevDiv/VS/a792c33b35dfffc5bd2d648861ffdb4b4ba34534/9743046/14760/1&amp;windowTitle=35017.276.dn-bot.240618.091029.558629&amp;path=TestExecutionOutputs/FileInteractionPerfTestsAsync.TestFileOpenAndSaveEmptyCSFile\" target=\"_blank\">📂 Open test outputs</a><br /><a href=\"https://aka.ms/VsEngDropExplorer?compareDrops=on&amp;currentDropName=Logs/DevDiv/VS/a792c33b35dfffc5bd2d648861ffdb4b4ba34534/9743046/14760/1&amp;baselineDropName=Logs/DevDiv/VS/ff59e9e0f35f2468d42fcdc177119906add38e50/9742981/14801/1&amp;folderName=FileInteractionPerfTestsAsync.TestFileOpenAndSaveEmptyCSFile\" target=\"_blank\">📈 Compare in PerfView</a> </td></tr>\r\n</table>\r\n</details>\r\n<br />\r\n\r\n\r\n---\r\n<details> \r\n<summary>📦 Common resources...</summary>\r\n<li><a href=\"http://ddoutages\">Active Outages</a></li>\r\n</details> <br />\r\n\r\n<details>\r\n<summary>🤔 How to...</summary>\r\n<li> <a href=\"https://aka.ms/RpsBrokenTestsGuidelines\">Investigate Broken Tests Issues</a><br /> </li>\r\n<li> <a href=\"https://aka.ms/RpsWalkthrus\">Investigate Performance Regressions issues</a><br /> </li>\r\n</details> <br />\r\n\r\n\r\n",
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
              Short: DDRIT: N/A, Speedometer: 0+2
              Long:
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
            Threads = "",
        }, """
            Display:
              Short: DDRIT: N/A, Speedometer: N/A
              Long:
                DDRIT: Not started
                Speedometer: Not started
            """);
    }

    [InlineSnapshotAssertion(parameterName: nameof(expected))]
    private static void Verify(Entry input, string? expected = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = -1)
    {
        var parser = new RpsParser();
        var rpsSummary = new RpsSummary();
        parser.ParseRpsSummary($$"""
            {
                "value": [{{input.Threads}}]
            }
            """, rpsSummary);
        InlineSnapshot.Validate(rpsSummary, expected, filePath, lineNumber);
    }
}
