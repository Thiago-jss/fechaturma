export type ClosureStatus = 'pending' | 'closed';
export type ClosureFilter = 'all' | ClosureStatus;

export function visibleClosures<T extends { status: ClosureStatus }>(
  closures: readonly T[], filter: ClosureFilter,
): readonly T[] {
  return filter === 'all' ? closures : closures.filter((closure) => closure.status === filter);
}
