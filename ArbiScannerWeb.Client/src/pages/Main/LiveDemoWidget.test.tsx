import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import LiveDemoWidget from './LiveDemoWidget';

vi.mock('react-apexcharts', () => ({ default: () => <div data-testid="apex-chart" /> }));
vi.mock('../../contexts/ThemeContext', () => ({ useTheme: () => ({ theme: 'light' }) }));

describe('LiveDemoWidget', () => {
    it('renders the live demo chart and order books using the demo data hook', () => {
        render(<LiveDemoWidget />);

        expect(screen.getByText('liveDemo.title')).toBeInTheDocument();
        expect(screen.getByTestId('apex-chart')).toBeInTheDocument();
        expect(screen.getAllByText('orderBlock.headers.side')).toHaveLength(2);
        expect(screen.getByText('Binance')).toBeInTheDocument();
        expect(screen.getByText('Bybit')).toBeInTheDocument();
    });
});
