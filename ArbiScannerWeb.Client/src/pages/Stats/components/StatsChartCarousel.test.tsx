import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import StatsChartCarousel, { type StatsChartPanel } from './StatsChartCarousel';

const panels: StatsChartPanel[] = [
    { key: 'a', title: 'Panel A', content: <div>Content A</div> },
    { key: 'b', title: 'Panel B', content: <div>Content B</div> },
    { key: 'c', title: 'Panel C', content: <div>Content C</div> },
];

describe('StatsChartCarousel', () => {
    beforeEach(() => {
        // jsdom does not implement scrollIntoView.
        Element.prototype.scrollIntoView = vi.fn();
    });

    it('renders nothing when there are no panels', () => {
        const { container } = render(<StatsChartCarousel panels={[]} />);
        expect(container).toBeEmptyDOMElement();
    });

    it('shows the first panel as active initially, with previous disabled', () => {
        render(<StatsChartCarousel panels={panels} />);

        expect(screen.getByText('Panel A')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'carousel.previous' })).toBeDisabled();
        expect(screen.getByRole('button', { name: 'carousel.next' })).not.toBeDisabled();
    });

    it('advances to the next panel and updates the title', async () => {
        render(<StatsChartCarousel panels={panels} />);

        await userEvent.click(screen.getByRole('button', { name: 'carousel.next' }));

        // The title crossfades via AnimatePresence (mode="wait"), so the new text only
        // appears once the previous title's exit animation finishes.
        expect(await screen.findByText('Panel B')).toBeInTheDocument();
        expect(Element.prototype.scrollIntoView).toHaveBeenCalled();
    });

    it('disables next on the last panel', async () => {
        render(<StatsChartCarousel panels={panels} />);

        await userEvent.click(screen.getByRole('button', { name: 'carousel.next' }));
        await userEvent.click(screen.getByRole('button', { name: 'carousel.next' }));

        expect(await screen.findByText('Panel C')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'carousel.next' })).toBeDisabled();
    });

    it('goes back to the previous panel', async () => {
        render(<StatsChartCarousel panels={panels} />);
        await userEvent.click(screen.getByRole('button', { name: 'carousel.next' }));

        await userEvent.click(screen.getByRole('button', { name: 'carousel.previous' }));

        expect(await screen.findByText('Panel A')).toBeInTheDocument();
    });

    it('jumps directly to a panel via its dot indicator', async () => {
        render(<StatsChartCarousel panels={panels} />);

        const dots = screen.getAllByRole('button', { name: 'carousel.goTo' });
        await userEvent.click(dots[2]);

        expect(await screen.findByText('Panel C')).toBeInTheDocument();
    });

    it('hides the dot indicators for a single panel', () => {
        render(<StatsChartCarousel panels={[panels[0]]} />);

        expect(screen.queryAllByRole('button', { name: 'carousel.goTo' })).toHaveLength(0);
    });
});
