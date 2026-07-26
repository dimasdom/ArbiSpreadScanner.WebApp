import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import FullPageLoader from './FullPageLoader';

describe('FullPageLoader', () => {
    it('renders a spinner without crashing', () => {
        const { container } = render(<FullPageLoader />);

        expect(container.querySelector('.animate-spin')).not.toBeNull();
    });
});
