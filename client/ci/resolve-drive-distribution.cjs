// Only GitHub API metadata decides which build and environment may be published.
module.exports = async function resolveDistribution(github, context) {
  const { owner, repo } = context.repo;
  const fullName = `${owner}/${repo}`;
  const { data: run } = await github.rest.actions.getWorkflowRun({
    owner, repo, run_id: context.payload.workflow_run.id,
  });
  if (run.status !== 'completed' || run.conclusion !== 'success' ||
      run.path !== '.github/workflows/client-ci.yml' ||
      run.repository.full_name !== fullName || run.head_repository?.full_name !== fullName) {
    return null;
  }
  let environment;
  let prNumber = '';
  if (run.event === 'pull_request') {
    if (run.pull_requests.length !== 1) return null;
    const { data: pr } = await github.rest.pulls.get({
      owner, repo, pull_number: run.pull_requests[0].number,
    });
    if (pr.state !== 'open' || pr.head.repo?.full_name !== fullName || pr.head.sha !== run.head_sha) {
      return null;
    }
    environment = 'preview';
    prNumber = String(pr.number);
  } else if (run.event === 'push' && run.head_branch === 'main') {
    environment = 'dev';
  } else if (run.event !== 'workflow_dispatch' ||
      run.head_branch !== context.payload.repository.default_branch) {
    return null;
  }
  const artifacts = await github.paginate(github.rest.actions.listWorkflowRunArtifacts, {
    owner, repo, run_id: run.id, per_page: 100,
  });
  const pattern = new RegExp(`^client-android-apk-(dev|prod|preview)-${run.run_attempt}$`);
  const matching = artifacts.filter(artifact => !artifact.expired && pattern.test(artifact.name));
  if (matching.length !== 1) throw new Error('Expected exactly one APK artifact for the completed run attempt.');
  const artifact = matching[0];
  const artifactEnvironment = pattern.exec(artifact.name)[1];
  if (environment && artifactEnvironment !== environment) {
    throw new Error('APK artifact environment does not match the CI event.');
  }
  if (!/^[a-f0-9]{40}$/.test(run.head_sha) || !Number.isSafeInteger(artifact.id) || artifact.id <= 0) {
    throw new Error('Invalid GitHub build metadata.');
  }
  return {
    environment: environment || artifactEnvironment,
    'artifact-id': String(artifact.id),
    'run-id': String(run.id),
    'head-sha': run.head_sha,
    'pr-number': prNumber,
  };
};
