import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import GuideModal, { type GuideStep } from './GuideModal';
import { createLocalStorageMock } from '../test/localStorageMock';

const twoSteps: GuideStep[] = [
    { icon: <span>icon-1</span>, title: 'Step One', description: 'First step' },
    { icon: <span>icon-2</span>, title: 'Step Two', description: 'Second step' },
];

describe('GuideModal', () => {
    let storage: Storage;

    beforeEach(() => {
        storage = createLocalStorageMock();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(storage);
    });

    it('shows the modal when the guide has not been seen', () => {
        render(<GuideModal storageKey="guide-a" title="Guide A" steps={twoSteps} />);

        expect(screen.getByText('Guide A')).toBeInTheDocument();
        expect(screen.getByText('Step One')).toBeInTheDocument();
    });

    it('does not show the modal when this guide was already seen', () => {
        storage.setItem('guide-a', 'true');

        render(<GuideModal storageKey="guide-a" title="Guide A" steps={twoSteps} />);

        expect(screen.queryByText('Guide A')).not.toBeInTheDocument();
    });

    it('does not show the modal when all guides are disabled', () => {
        storage.setItem('guides-all-disabled', 'true');

        render(<GuideModal storageKey="guide-a" title="Guide A" steps={twoSteps} />);

        expect(screen.queryByText('Guide A')).not.toBeInTheDocument();
    });

    it('advances to the next step and back again', async () => {
        render(<GuideModal storageKey="guide-a" title="Guide A" steps={twoSteps} />);

        await userEvent.click(screen.getByRole('button', { name: 'guide.next' }));
        expect(screen.getByText('Step Two')).toBeInTheDocument();

        await userEvent.click(screen.getByRole('button', { name: 'guide.back' }));
        expect(screen.getByText('Step One')).toBeInTheDocument();
    });

    it('jumps to a step via the progress dots', async () => {
        render(<GuideModal storageKey="guide-a" title="Guide A" steps={twoSteps} />);

        const dots = screen.getAllByRole('button', { name: 'guide.goToStep' });
        await userEvent.click(dots[1]);
        expect(screen.getByText('Step Two')).toBeInTheDocument();
    });

    it('closes and records this guide as seen when "got it" is clicked on the last step', async () => {
        render(<GuideModal storageKey="guide-a" title="Guide A" steps={twoSteps} />);
        await userEvent.click(screen.getByRole('button', { name: 'guide.next' }));

        await userEvent.click(screen.getByRole('button', { name: 'guide.gotIt' }));

        expect(screen.queryByText('Guide A')).not.toBeInTheDocument();
        expect(storage.getItem('guide-a')).toBe('true');
    });

    it('closes and disables all guides when "don\'t show again" is clicked', async () => {
        render(<GuideModal storageKey="guide-a" title="Guide A" steps={twoSteps} />);

        await userEvent.click(screen.getByRole('button', { name: 'guide.dontShowAgain' }));

        expect(screen.queryByText('Guide A')).not.toBeInTheDocument();
        expect(storage.getItem('guides-all-disabled')).toBe('true');
    });

    it('closes without recording anything when the backdrop is clicked', async () => {
        render(<GuideModal storageKey="guide-a" title="Guide A" steps={twoSteps} />);

        const closeButtons = screen.getAllByRole('button', { name: 'guide.close' });
        await userEvent.click(closeButtons[0]);

        expect(screen.queryByText('Guide A')).not.toBeInTheDocument();
        expect(storage.getItem('guide-a')).toBeNull();
    });

    it('hides the progress dots and back button, and shows "got it" for a single-step guide', () => {
        render(<GuideModal storageKey="guide-b" title="Guide B" steps={[twoSteps[0]]} />);

        expect(screen.queryByRole('button', { name: 'guide.back' })).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: 'guide.goToStep' })).not.toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'guide.gotIt' })).toBeInTheDocument();
    });
});
