// @ts-check
/** @param {import('github-script').AsyncFunctionArguments} AsyncFunctionArguments */
export default async ({ context, github }) => {
    console.log(context);

    const pulls = await github.rest.pulls.list({ 
        owner: context.repo.owner,
        repo: context.repo.repo,
    });

    for (const pull of pulls.data) {
        if (pull.number !== context.issue.number) {
            console.log(`Removing label from pull request #${pull.id}`);
            await github.rest.issues.removeLabel({
                owner: context.repo.owner,
                repo: context.repo.repo,
                issue_number: pull.number,
                name: 'deploy: staging',
            });
        }
    }
};
