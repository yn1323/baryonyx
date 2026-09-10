export function days(steps = 123) {
  const now = new Date();
  const today = new Date(now.toISOString().slice(0, 10));
  return Array.from({ length: 7 }, (_, index) => {
    const start = new Date(today.getTime() - (6 - index) * 86_400_000);
    const end = index === 6 ? now : new Date(start.getTime() + 86_400_000);
    return {
      day: start.toISOString().slice(0, 10),
      zone: "UTC",
      startAt: start.toISOString(),
      endAt: end.toISOString(),
      hasValue: true,
      steps,
      observedAt: now.toISOString(),
    };
  });
}
