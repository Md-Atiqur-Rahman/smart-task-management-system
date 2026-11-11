import { routes } from './app.routes';

describe('app routes', () => {
  it('has a not-found page inside the signed-in shell, where roleGuard sends a wrong role', () => {
    const shell = routes.find((route) => route.path === '' && route.children);

    expect(shell?.children?.some((child) => child.path === 'not-found')).toBe(true);
  });
});
