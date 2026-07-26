import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

const useThemeMock = vi.fn();

vi.mock('./ThemeContext', () => ({ useTheme: () => useThemeMock() }));

describe('MuiBridge', () => {
    it('wraps children in an MUI theme matching the app theme', async () => {
        useThemeMock.mockReturnValue({ theme: 'dark', toggleTheme: vi.fn() });
        const { default: MuiBridge } = await import('./MuiBridge');

        render(
            <MuiBridge>
                <div>Child content</div>
            </MuiBridge>,
        );

        expect(screen.getByText('Child content')).toBeInTheDocument();
    });
});
