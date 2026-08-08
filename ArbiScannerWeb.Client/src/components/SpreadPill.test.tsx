import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import SpreadPill from './SpreadPill';

describe('SpreadPill', () => {
    it('renders a positive spread with green styling', () => {
        render(<SpreadPill spreadPercent={2.5} />);

        const pill = screen.getByText('2.50%');
        expect(pill).toBeInTheDocument();
        expect(pill.className).toContain('green');
    });

    it('renders a negative spread with red styling', () => {
        render(<SpreadPill spreadPercent={-1.234} />);

        const pill = screen.getByText('-1.23%');
        expect(pill.className).toContain('red');
    });
});
