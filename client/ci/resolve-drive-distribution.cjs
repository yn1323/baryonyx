// The calling build supplies its exact artifact ID; GitHub confirms its source.
module.exports = async function resolveDistribution(github, context, input) {
  const { owner, repo } = context.repo;
  const fullName = `${owner}/${repo}`;
  const { data: run } = await github.rest.actions.getWorkflowRun({
    owner, repo, run_id: context.runId,
  });
  if ((run.status !== 'in_progress' &&
      !(run.status === 'completed' && run.conclusion === 'success')) ||
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
    environment = 'prod';
  } else if (run.event === 'push' && ['dev', 'develop'].includes(run.head_branch)) {
    environment = 'dev';
  } else if (run.event !== 'workflow_dispatch' ||
      !['main', 'dev', 'develop'].includes(run.head_branch)) {
    return null;
  }
  if (!['dev', 'prod', 'preview'].includes(input.environment) ||
      (environment && environment !== input.environment)) {
    throw new Error('APK environment does not match the CI event.');
  }
  const artifactId = Number(input.artifactId);
  if (!Number.isSafeInteger(artifactId) || artifactId <= 0) {
    throw new Error('Invalid APK artifact ID from the Android build.');
  }
  const artifacts = await github.paginate(github.rest.actions.listWorkflowRunArtifacts, {
    owner, repo, run_id: run.id, per_page: 100,
  });
  // A retry of only this job keeps the artifact ID from the successful build.
  const pattern = /^client-android-apk-(dev|prod|preview)-([1-9]\d*)$/;
  const matching = artifacts.filter(artifact => !artifact.expired && artifact.id === artifactId);
  if (matching.length !== 1 || !pattern.test(matching[0].name)) {
    throw new Error('Expected exactly one APK artifact from the successful Android build.');
  }
  const artifact = matching[0];
  const artifactEnvironment = pattern.exec(artifact.name)[1];
  if (artifactEnvironment !== input.environment) {
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
