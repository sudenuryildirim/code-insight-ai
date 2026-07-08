export type IssueSeverity = 'Info' | 'Warning' | 'Error' | 'Critical';

export type IssueCategory =
  | 'Bug'
  | 'Security'
  | 'Performance'
  | 'SOLID'
  | 'CleanCode'
  | 'Refactoring'
  | 'CodeSmell'
  | 'Testing'
  | 'General';

export interface ReviewIssue {
  filePath: string;
  lineNumber: number;
  lineContent: string;
  severity: IssueSeverity;
  category: IssueCategory;
  title: string;
  description: string;
  suggestion: string;
  refactoredCode: string;
}

export interface CodeReviewReport {
  id: string;
  fileName: string;
  detectedLanguage: string;
  score: number;
  codePurpose: string;
  summary: string;
  strengths: string[];
  issues: ReviewIssue[];
  recommendations: string[];
  createdAt: string;
}

export interface PullRequestSummary {
  number: number;
  title: string;
  author: string;
  baseBranch: string;
  headBranch: string;
  url: string;
  updatedAt: string;
}

// The system never approves/merges a PR - it only reports; the merge decision is always made
// by a human on GitHub. "verdict" is a short summary label, not an approval decision.
export interface PullRequestReport {
  id: string;
  repoOwner: string;
  repoName: string;
  prNumber: number;
  prTitle: string;
  prUrl: string;
  author: string;
  baseBranch: string;
  headBranch: string;
  detectedPurpose: string;
  reliabilityScore: number;
  verdict: string;
  summary: string;
  issues: ReviewIssue[];
  recommendations: string[];
  createdAt: string;
}
